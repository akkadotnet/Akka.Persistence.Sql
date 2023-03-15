// -----------------------------------------------------------------------
//  <copyright file="Linq2DbWriteJournal.cs" company="Akka.NET Project">
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

    public class Linq2DbWriteJournal : AsyncWriteJournal
    {
        [Obsolete(message: "Use Linq2DbPersistence.DefaultConfiguration or Linq2DbPersistence.Get(ActorSystem).DefaultConfig instead")]
        public static readonly Configuration.Config DefaultConfiguration = Linq2DbPersistence.DefaultConfiguration;

        private readonly ByteArrayJournalDao _journal;
        private readonly JournalConfig _journalConfig;
        private readonly ILoggingAdapter _log;

        private readonly ActorMaterializer _mat;

        private readonly Dictionary<string, Task> _writeInProgress = new();

        public Linq2DbWriteJournal(Configuration.Config journalConfig)
        {
            _log = Context.GetLogger();

            try
            {
                var config = journalConfig.WithFallback(Linq2DbPersistence.DefaultJournalConfiguration);
                _journalConfig = new JournalConfig(config);

                _mat = Materializer.CreateSystemMaterializer(
                    context: (ExtendedActorSystem)Context.System,
                    settings: ActorMaterializerSettings
                        .Create(Context.System)
                        .WithDispatcher(_journalConfig.MaterializerDispatcher),
                    namePrefix: "l2dbWriteJournal");

                try
                {
                    _journal = new ByteArrayJournalDao(
                        scheduler: Context.System.Scheduler.Advanced,
                        mat: _mat,
                        connection: new AkkaPersistenceDataConnectionFactory(_journalConfig),
                        journalConfig: _journalConfig,
                        serializer: Context.System.Serialization,
                        logger: Context.GetLogger());
                }
                catch (Exception e)
                {
                    Context.GetLogger().Error(e, "Error Initializing Journal!");
                    throw;
                }

                if (!_journalConfig.AutoInitialize)
                    return;

                try
                {
                    _journal.InitializeTables();
                }
                catch (Exception e)
                {
                    Context.GetLogger().Warning(e, "Unable to Initialize Persistence Journal Table!");
                }
            }
            catch (Exception ex)
            {
                _log.Warning(ex, "Unexpected error initializing journal!");
                throw;
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
            _log.Error(cause, $"Linq2Db Journal Error on {message?.GetType().ToString() ?? "null"}");
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
                cancellationToken: CancellationToken.None,
                continuationOptions: TaskContinuationOptions.ExecuteSynchronously,
                scheduler: TaskScheduler.Default);

            // But we still want to return the future from `AsyncWriteMessages`
            return future;
        }

        protected override async Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
            => await _journal.Delete(persistenceId, toSequenceNr);
    }
}
