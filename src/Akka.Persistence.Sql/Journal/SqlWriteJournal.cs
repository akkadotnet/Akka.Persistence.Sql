// -----------------------------------------------------------------------
//  <copyright file="SqlWriteJournal.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence.Journal;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Dao;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Utility;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;

namespace Akka.Persistence.Sql.Journal
{
    public class DateTimeHelpers
    {
        private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long ToUnixEpochMillis(DateTime time)
            => (long)(time.ToUniversalTime() - UnixEpoch).TotalMilliseconds;

        public static long UnixEpochMillis()
            => (long)(DateTime.UtcNow - UnixEpoch).TotalMilliseconds;

        public static DateTime FromUnixEpochMillis(in long unixEpochMillis)
            => UnixEpoch.AddMilliseconds(unixEpochMillis);
    }

    public class SqlWriteJournal : AsyncWriteJournal, IWithUnboundedStash
    {
        [Obsolete(message: "Use SqlPersistence.DefaultConfiguration or SqlPersistence.Get(ActorSystem).DefaultConfig instead")]
        public static readonly Configuration.Config DefaultConfiguration = SqlPersistence.DefaultConfiguration;

        private ByteArrayJournalDao _journal;
        private readonly JournalConfig _journalConfig;
        private readonly ILoggingAdapter _log;

        private ActorMaterializer _mat;

        private readonly Dictionary<string, Task> _writeInProgress = new();
        private readonly CancellationTokenSource _pendingWriteCts;

        public IStash Stash { get; set; }

        public SqlWriteJournal(Configuration.Config journalConfig)
        {
            _log = Context.GetLogger();
            _pendingWriteCts = new CancellationTokenSource();

            var config = journalConfig.WithFallback(SqlPersistence.DefaultJournalConfiguration);
            _journalConfig = new JournalConfig(config);
        }

        protected override void PreStart()
        {
            base.PreStart();
            Initialize().PipeTo(Self);
            BecomeStacked(Initializing);
        }

        protected override void PostStop()
        {
            base.PostStop();
            _pendingWriteCts.Cancel();
            _pendingWriteCts.Dispose();
        }

        private async Task<Status> Initialize()
        {
            try
            {
                _mat = Materializer.CreateSystemMaterializer(
                    context: (ExtendedActorSystem)Context.System,
                    settings: ActorMaterializerSettings
                        .Create(Context.System)
                        .WithDispatcher(_journalConfig.MaterializerDispatcher),
                    namePrefix: "l2dbWriteJournal");

                _journal = new ByteArrayJournalDao(
                    scheduler: Context.System.Scheduler.Advanced,
                    mat: _mat,
                    connection: new AkkaPersistenceDataConnectionFactory(_journalConfig),
                    journalConfig: _journalConfig,
                    serializer: Context.System.Serialization,
                    logger: Context.GetLogger(),
                    shutdownToken: _pendingWriteCts.Token);
                
                if (!_journalConfig.AutoInitialize)
                    return Status.Success.Instance;
                
                await _journal.InitializeTables(_pendingWriteCts.Token);
            }
            catch (Exception e)
            {
                return new Status.Failure(e);
            }
            
            return Status.Success.Instance;
        }

        private bool Initializing(object message)
        {
            switch (message)
            {
                case Status.Success:
                    UnbecomeStacked();
                    Stash.UnstashAll();
                    return true;
                case Status.Failure fail:
                    _log.Error(fail.Cause, "Failure during {0} initialization.", Self);
                    Context.Stop(Self);
                    return true;
                default:
                    Stash.Stash();
                    return true;
            }
        }
        
        protected override bool ReceivePluginInternal(object message)
        {
            if (message is not WriteFinished wf)
                return false;

            if (_writeInProgress.TryGetValue(wf.PersistenceId, out var latestPending) & (latestPending == wf.Future))
                _writeInProgress.Remove(wf.PersistenceId);

            return true;
        }

        public override void AroundPreRestart(Exception cause, object message)
        {
            _log.Error(cause, $"Sql Journal Error on {message?.GetType().ToString() ?? "null"}");
            base.AroundPreRestart(cause, message);
        }

        public override async Task ReplayMessagesAsync(
            IActorContext context,
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            long max,
            Action<IPersistentRepresentation> recoveryCallback)
            => await _journal
                .MessagesWithBatch(
                    persistenceId: persistenceId,
                    fromSequenceNr: fromSequenceNr,
                    toSequenceNr: toSequenceNr,
                    batchSize: _journalConfig.DaoConfig.ReplayBatchSize,
                    refreshInterval: Option<(TimeSpan, IScheduler)>.None)
                .Take(n: max)
                .SelectAsync(
                    parallelism: 1,
                    asyncMapper: t => t.IsSuccess
                        ? Task.FromResult(t.Success.Value)
                        : Task.FromException<ReplayCompletion>(t.Failure.Value))
                .RunForeach(r => recoveryCallback(r.Repr), _mat);

        public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            if (_writeInProgress.TryGetValue(persistenceId, out var wip))
            {
                // We don't care whether the write succeeded or failed
                // We just want it to finish.
                await new NoThrowAwaiter(wip);
            }

            return await _journal.HighestSequenceNr(persistenceId, fromSequenceNr);
        }

        protected override Task<IImmutableList<Exception>> WriteMessagesAsync(IEnumerable<AtomicWrite> messages)
        {
            // TODO: CurrentTimeMillis;
            var currentTime = DateTime.UtcNow.Ticks;

            var messagesList = messages.ToList();
            var persistenceId = messagesList.Head().PersistenceId;

            var future = _journal.AsyncWriteMessages(messagesList, currentTime);

            _writeInProgress[persistenceId] = future;
            var self = Self;

            // When we are done, we want to send a 'WriteFinished' so that
            // Sequence Number reads won't block/await/etc.
            future.ContinueWith(
                continuationAction: p => self.Tell(new WriteFinished(persistenceId, p)),
                cancellationToken: _pendingWriteCts.Token,
                continuationOptions: TaskContinuationOptions.ExecuteSynchronously,
                scheduler: TaskScheduler.Default);

            // But we still want to return the future from `AsyncWriteMessages`
            return future;
        }

        protected override async Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
            => await _journal.Delete(persistenceId, toSequenceNr);
    }
}
