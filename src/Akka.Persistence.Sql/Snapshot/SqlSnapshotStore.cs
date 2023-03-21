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
    public class SqlSnapshotStore : SnapshotStore
    {
        [Obsolete(message: "Use SqlPersistence.Get(ActorSystem).DefaultConfig instead")]
        public static readonly Configuration.Config DefaultConfiguration =
            ConfigurationFactory.FromResource<SqlSnapshotStore>("Akka.Persistence.Sql.snapshot.conf");

        private readonly ByteArraySnapshotDao _dao;

        public SqlSnapshotStore(Configuration.Config snapshotConfig)
        {
            var config = snapshotConfig.WithFallback(SqlPersistence.DefaultSnapshotConfiguration);

            var snapshotConfig1 = new SnapshotConfig(config);

            _dao = new ByteArraySnapshotDao(
                connectionFactory: new AkkaPersistenceDataConnectionFactory(snapshotConfig1),
                snapshotConfig: snapshotConfig1,
                serialization: Context.System.Serialization,
                mat: Materializer.CreateSystemMaterializer((ExtendedActorSystem)Context.System),
                logger: Context.GetLogger());

            if (!snapshotConfig1.AutoInitialize)
                return;

            try
            {
                _dao.InitializeTables();
            }
            catch (Exception e)
            {
                Context.GetLogger().Warning(
                    e,
                    "Unable to Initialize Persistence Snapshot Table!");
            }
        }

        protected override async Task<SelectedSnapshot> LoadAsync(
            string persistenceId,
            SnapshotSelectionCriteria criteria)
            => criteria.MaxSequenceNr switch
            {
                long.MaxValue when criteria.MaxTimeStamp == DateTime.MaxValue
                    => (await _dao.LatestSnapshot(persistenceId)).GetOrElse(null),

                long.MaxValue
                    => (await _dao.SnapshotForMaxTimestamp(persistenceId, criteria.MaxTimeStamp)).GetOrElse(null),

                _ => criteria.MaxTimeStamp == DateTime.MaxValue
                    ? (await _dao.SnapshotForMaxSequenceNr(
                        persistenceId: persistenceId,
                        sequenceNr: criteria.MaxSequenceNr)).GetOrElse(null)
                    : (await _dao.SnapshotForMaxSequenceNrAndMaxTimestamp(
                        persistenceId: persistenceId,
                        sequenceNr: criteria.MaxSequenceNr,
                        timestamp: criteria.MaxTimeStamp)).GetOrElse(null)
            };

        protected override async Task SaveAsync(SnapshotMetadata metadata, object snapshot)
            => await _dao.Save(metadata, snapshot);

        protected override async Task DeleteAsync(SnapshotMetadata metadata)
            => await _dao.Delete(metadata.PersistenceId, metadata.SequenceNr);

        protected override async Task DeleteAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            switch (criteria.MaxSequenceNr)
            {
                case long.MaxValue when criteria.MaxTimeStamp == DateTime.MaxValue:
                    await _dao.DeleteAllSnapshots(persistenceId);
                    break;

                case long.MaxValue:
                    await _dao.DeleteUpToMaxTimestamp(persistenceId, criteria.MaxTimeStamp);
                    break;

                default:
                {
                    if (criteria.MaxTimeStamp == DateTime.MaxValue)
                    {
                        await _dao.DeleteUpToMaxSequenceNr(persistenceId, criteria.MaxSequenceNr);
                    }
                    else
                    {
                        await _dao.DeleteUpToMaxSequenceNrAndMaxTimestamp(
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
