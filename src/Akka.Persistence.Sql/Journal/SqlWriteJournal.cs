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

        private readonly JournalConfig _journalConfig;
        private readonly ILoggingAdapter _log;
        private readonly CancellationTokenSource _pendingWriteCts;
        private readonly bool _useWriterUuid;

        private readonly Dictionary<string, Task> _writeInProgress = new();

        private ByteArrayJournalDao? _journal;

        private ActorMaterializer? _mat;

        public SqlWriteJournal(Configuration.Config journalConfig)
        {
            _log = Context.GetLogger();
            _pendingWriteCts = new CancellationTokenSource();

            var config = journalConfig.WithFallback(SqlPersistence.DefaultJournalConfiguration);
            _journalConfig = new JournalConfig(config);

            var setup = Context.System.Settings.Setup;
            var singleSetup = setup.Get<DataOptionsSetup>();
            if (singleSetup.HasValue)
                _journalConfig = singleSetup.Value.Apply(_journalConfig);
            
            if (_journalConfig.PluginId is not null)
            {
                var multiSetup = setup.Get<MultiDataOptionsSetup>();
                if (multiSetup.HasValue && multiSetup.Value.TryGetDataOptionsFor(_journalConfig.PluginId, out var dataOptions))
                    _journalConfig = _journalConfig.WithDataOptions(dataOptions);
            }

            _useWriterUuid = _journalConfig.TableConfig.EventJournalTable.UseWriterUuidColumn;
        }

        // Stash is needed because we need to stash all incoming messages while we're waiting for the
        // journal DAO to be properly initialized.
        public IStash Stash { get; set; } = null!;

        protected override void PreStart()
        {
            base.PreStart();
            Initialize().PipeTo(Self);

            // We have to use BecomeStacked here because the default Receive method is sealed in the
            // base class and it uses a custom code to handle received messages.
            // We need to suspend the base class behavior while we're waiting for the journal DAO to be properly
            // initialized.
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
                    selfUuid: _useWriterUuid
                        ? Guid.NewGuid().ToString("N")
                        : null,
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
                    // trigger a restart so we have some hope of succeeding in the future even if initialization failed
                    throw new ApplicationException("Failed to initialize SQL Journal.", fail.Cause);
                default:
                    Stash.Stash();
                    return true;
            }
        }

        protected override bool ReceivePluginInternal(object message)
        {
            switch (message)
            {
                case WriteFinished wf:
                    if (_writeInProgress.TryGetValue(wf.PersistenceId, out var latestPending) & (latestPending == wf.Future))
                        _writeInProgress.Remove(wf.PersistenceId);
                    return true;

                // `IsInitialized` and `Initialized` are used mostly for testing purposes,
                // to make sure that the write journal has been initialized before we
                // start the query read journal tests.
                case IsInitialized:
                    Sender.Tell(Initialized.Instance);
                    return true;

                default:
                    return false;
            }
        }

        public override void AroundPreRestart(Exception cause, object? message)
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
            => await _journal!
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
                .RunForeach(r => recoveryCallback(r.Representation), _mat);

        public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            if (_writeInProgress.TryGetValue(persistenceId, out var wip))
            {
                // We don't care whether the write succeeded or failed
                // We just want it to finish.
                await new NoThrowAwaiter(wip);
            }

            return await _journal!.HighestSequenceNr(persistenceId, fromSequenceNr);
        }

        protected override Task<IImmutableList<Exception>> WriteMessagesAsync(IEnumerable<AtomicWrite> messages)
        {
            // TODO: CurrentTimeMillis;
            var currentTime = DateTime.UtcNow.Ticks;

            var messagesList = messages.ToList();
            var persistenceId = messagesList.Head().PersistenceId;

            var future = _journal!.AsyncWriteMessages(messagesList, currentTime);

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
            => await _journal!.Delete(persistenceId, toSequenceNr);
    }
}
