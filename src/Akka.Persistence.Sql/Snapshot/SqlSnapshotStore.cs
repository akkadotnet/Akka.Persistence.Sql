// -----------------------------------------------------------------------
//  <copyright file="SqlSnapshotStore.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Persistence.Snapshot;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Utility;
using Akka.Streams;

namespace Akka.Persistence.Sql.Snapshot
{
    public class SqlSnapshotStore : SnapshotStore, IWithUnboundedStash
    {
        // ReSharper disable once UnusedMember.Global
        [Obsolete(message: "Use SqlPersistence.Get(ActorSystem).DefaultConfig instead")]
        public static readonly Configuration.Config DefaultConfiguration =
            ConfigurationFactory.FromResource<SqlSnapshotStore>("Akka.Persistence.Sql.snapshot.conf");

        private readonly ByteArraySnapshotDao _dao;
        private readonly ILoggingAdapter _log;
        private readonly SnapshotConfig _settings;
        private readonly CancellationTokenSource _initializationCts = new();
        private readonly TimeSpan _initializationRetryDelay;

        private sealed class InitializationResult : INoSerializationVerificationNeeded
        {
            public InitializationResult(SqlSnapshotStore owner, Status status)
            {
                Owner = owner;
                Status = status;
            }

            public SqlSnapshotStore Owner { get; }
            public Status Status { get; }
        }

        public SqlSnapshotStore(Configuration.Config snapshotConfig)
        {
            _log = Context.GetLogger();

            var config = snapshotConfig.WithFallback(SqlPersistence.DefaultSnapshotConfiguration);
            _settings = new SnapshotConfig(config);
            _initializationRetryDelay = config.GetTimeSpan("initialization-retry-delay");
            if (_initializationRetryDelay < TimeSpan.FromMilliseconds(1) ||
                _initializationRetryDelay > TimeSpan.FromMilliseconds(int.MaxValue - 1))
                throw new ArgumentOutOfRangeException("initialization-retry-delay", "Initialization retry delay must be between 1 ms and the maximum supported Task.Delay duration.");

            var setup = Context.System.Settings.Setup;
            var singleSetup = setup.Get<DataOptionsSetup>();
            if (singleSetup.HasValue)
                _settings = singleSetup.Value.Apply(_settings);

            if (_settings.PluginId is not null)
            {
                var multiSetup = setup.Get<MultiDataOptionsSetup>();
                if (multiSetup.HasValue && multiSetup.Value.TryGetDataOptionsFor(_settings.PluginId, out var dataOptions))
                    _settings = _settings.WithDataOptions(dataOptions!);
            }

            _dao = new ByteArraySnapshotDao(
                connectionFactory: new AkkaPersistenceDataConnectionFactory(_settings),
                snapshotConfig: _settings,
                serialization: Context.System.Serialization,
                materializer: Context.Materializer(),
                logger: Context.GetLogger());
        }

        public IStash Stash { get; set; } = null!;

        protected override void PreStart()
        {
            base.PreStart();
            _ = InitializeAndNotify(Self);
            BecomeStacked(WaitingForInitialization);
        }

        protected override void PostStop()
        {
            try
            {
                try
                {
                    _initializationCts.Cancel();
                }
                catch (Exception e)
                {
                    _log.Error(e, "Failure cancelling SQL SnapshotStore initialization during shutdown.");
                }
            }
            finally
            {
                try
                {
                    _initializationCts.Dispose();
                }
                catch (Exception e)
                {
                    _log.Error(e, "Failure disposing SQL SnapshotStore cancellation source during shutdown.");
                }
                finally
                {
                    try
                    {
                        _dao.Dispose();
                    }
                    catch (Exception e)
                    {
                        _log.Error(e, "Failure disposing SQL SnapshotStore DAO during shutdown.");
                    }
                    finally
                    {
                        base.PostStop();
                    }
                }
            }
        }

        private bool WaitingForInitialization(object message)
        {
            if (message is InitializationResult result)
            {
                if (!ReferenceEquals(result.Owner, this))
                    return true;
                message = result.Status;
            }

            switch (message)
            {
                case Status.Success:
                    UnbecomeStacked();
                    Stash.UnstashAll();
                    return true;

                case Status.Failure msg:
                    // trigger a restart so we have some hope of succeeding in the future even if initialization failed
                    throw new ApplicationException("Failed to initialize SQL SnapshotStore.", msg.Cause);

                default:
                    Stash.Stash();
                    return true;
            }
        }

        private async Task<Status> Initialize()
        {
            var cancellationToken = _initializationCts.Token;
            if (!_settings.AutoInitialize)
                return new Status.Success(NotUsed.Instance);

            try
            {
                await _dao.InitializeTables();
            }
            catch (Exception e)
            {
                _log.Error(e, "Failure during SQL SnapshotStore initialization.");
                await Task.Delay(_initializationRetryDelay, cancellationToken);
                return new Status.Failure(e);
            }

            return Status.Success.Instance;
        }

        private async Task InitializeAndNotify(IActorRef self)
        {
            try
            {
                var status = await Initialize();
                if (!_initializationCts.IsCancellationRequested)
                    self.Tell(new InitializationResult(this, status));
            }
            catch (OperationCanceledException) when (_initializationCts.IsCancellationRequested)
            {
                // This incarnation was stopped while waiting to retry initialization.
            }
            catch (Exception e)
            {
                _log.Error(e, "Failure during SQL SnapshotStore initialization.");
                if (!_initializationCts.IsCancellationRequested)
                    self.Tell(new InitializationResult(this, new Status.Failure(e)));
            }
        }

        protected override async Task<SelectedSnapshot?> LoadAsync(
            string persistenceId,
            SnapshotSelectionCriteria criteria, 
            CancellationToken cancellationToken)
            => criteria.MaxSequenceNr switch
            {
                long.MaxValue when criteria.MaxTimeStamp == DateTime.MaxValue
                    => (await _dao.LatestSnapshotAsync(persistenceId, cancellationToken)).GetOrNull(),

                long.MaxValue
                    => (await _dao.SnapshotForMaxTimestampAsync(persistenceId, criteria.MaxTimeStamp, cancellationToken)).GetOrNull(),

                _ => criteria.MaxTimeStamp == DateTime.MaxValue
                    ? (await _dao.SnapshotForMaxSequenceNrAsync(
                        persistenceId: persistenceId,
                        sequenceNr: criteria.MaxSequenceNr, 
                        cancellationToken: cancellationToken)).GetOrNull()
                    : (await _dao.SnapshotForMaxSequenceNrAndMaxTimestampAsync(
                        persistenceId: persistenceId,
                        sequenceNr: criteria.MaxSequenceNr,
                        timestamp: criteria.MaxTimeStamp, 
                        cancellationToken: cancellationToken)).GetOrNull(),
            };

        protected override async Task SaveAsync(SnapshotMetadata metadata, object snapshot, CancellationToken cancellationToken)
            => await _dao.SaveAsync(metadata, snapshot, cancellationToken);

        protected override async Task DeleteAsync(SnapshotMetadata metadata, CancellationToken cancellationToken)
            => await _dao.DeleteAsync(metadata.PersistenceId, metadata.SequenceNr, metadata.Timestamp, cancellationToken);

        protected override async Task DeleteAsync(string persistenceId, SnapshotSelectionCriteria criteria, CancellationToken cancellationToken)
        {
            switch (criteria.MaxSequenceNr)
            {
                case long.MaxValue when criteria.MaxTimeStamp == DateTime.MaxValue:
                    await _dao.DeleteAllSnapshotsAsync(persistenceId, cancellationToken);
                    break;

                case long.MaxValue:
                    await _dao.DeleteUpToMaxTimestampAsync(persistenceId, criteria.MaxTimeStamp, cancellationToken);
                    break;

                default:
                {
                    if (criteria.MaxTimeStamp == DateTime.MaxValue)
                    {
                        await _dao.DeleteUpToMaxSequenceNrAsync(persistenceId, criteria.MaxSequenceNr, cancellationToken);
                    }
                    else
                    {
                        await _dao.DeleteUpToMaxSequenceNrAndMaxTimestampAsync(
                            persistenceId: persistenceId,
                            maxSequenceNr: criteria.MaxSequenceNr,
                            maxTimestamp: criteria.MaxTimeStamp, 
                            cancellationToken: cancellationToken);
                    }

                    break;
                }
            }
        }
    }
}
