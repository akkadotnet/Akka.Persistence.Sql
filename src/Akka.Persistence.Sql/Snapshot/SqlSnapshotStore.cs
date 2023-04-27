// -----------------------------------------------------------------------
//  <copyright file="SqlSnapshotStore.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Persistence.Snapshot;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Utility;

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

        public SqlSnapshotStore(Configuration.Config snapshotConfig)
        {
            _log = Context.GetLogger();

            var config = snapshotConfig.WithFallback(SqlPersistence.DefaultSnapshotConfiguration);
            _settings = new SnapshotConfig(config);

            _dao = new ByteArraySnapshotDao(
                connectionFactory: new AkkaPersistenceDataConnectionFactory(_settings),
                snapshotConfig: _settings,
                serialization: Context.System.Serialization,
                materializer: Materializer.CreateSystemMaterializer((ExtendedActorSystem)Context.System),
                logger: Context.GetLogger());
        }

        public IStash Stash { get; set; }

        protected override void PreStart()
        {
            base.PreStart();
            Initialize().PipeTo(Self);
            BecomeStacked(WaitingForInitialization);
        }

        private bool WaitingForInitialization(object message)
        {
            switch (message)
            {
                case Status.Success:
                    UnbecomeStacked();
                    Stash.UnstashAll();
                    return true;

                case Status.Failure msg:
                    _log.Error(msg.Cause, "Error during {0} initialization", Self);
                    Context.Stop(Self);
                    return true;

                default:
                    Stash.Stash();
                    return true;
            }
        }

        private async Task<Status> Initialize()
        {
            if (!_settings.AutoInitialize)
                return new Status.Success(NotUsed.Instance);

            try
            {
                await _dao.InitializeTables();
            }
            catch (Exception e)
            {
                return new Status.Failure(e);
            }

            return Status.Success.Instance;
        }

        protected override async Task<SelectedSnapshot> LoadAsync(
            string persistenceId,
            SnapshotSelectionCriteria criteria)
            => criteria.MaxSequenceNr switch
            {
                long.MaxValue when criteria.MaxTimeStamp == DateTime.MaxValue
                    => (await _dao.LatestSnapshotAsync(persistenceId)).GetOrElse(null),

                long.MaxValue
                    => (await _dao.SnapshotForMaxTimestampAsync(persistenceId, criteria.MaxTimeStamp)).GetOrElse(null),

                _ => criteria.MaxTimeStamp == DateTime.MaxValue
                    ? (await _dao.SnapshotForMaxSequenceNrAsync(
                        persistenceId: persistenceId,
                        sequenceNr: criteria.MaxSequenceNr)).GetOrElse(null)
                    : (await _dao.SnapshotForMaxSequenceNrAndMaxTimestampAsync(
                        persistenceId: persistenceId,
                        sequenceNr: criteria.MaxSequenceNr,
                        timestamp: criteria.MaxTimeStamp)).GetOrElse(null),
            };

        protected override async Task SaveAsync(SnapshotMetadata metadata, object snapshot)
            => await _dao.SaveAsync(metadata, snapshot);

        protected override async Task DeleteAsync(SnapshotMetadata metadata)
            => await _dao.DeleteAsync(metadata.PersistenceId, metadata.SequenceNr);

        protected override async Task DeleteAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            switch (criteria.MaxSequenceNr)
            {
                case long.MaxValue when criteria.MaxTimeStamp == DateTime.MaxValue:
                    await _dao.DeleteAllSnapshotsAsync(persistenceId);
                    break;

                case long.MaxValue:
                    await _dao.DeleteUpToMaxTimestampAsync(persistenceId, criteria.MaxTimeStamp);
                    break;

                default:
                {
                    if (criteria.MaxTimeStamp == DateTime.MaxValue)
                    {
                        await _dao.DeleteUpToMaxSequenceNrAsync(persistenceId, criteria.MaxSequenceNr);
                    }
                    else
                    {
                        await _dao.DeleteUpToMaxSequenceNrAndMaxTimestampAsync(
                            persistenceId: persistenceId,
                            maxSequenceNr: criteria.MaxSequenceNr,
                            maxTimestamp: criteria.MaxTimeStamp);
                    }

                    break;
                }
            }
        }
    }
}
