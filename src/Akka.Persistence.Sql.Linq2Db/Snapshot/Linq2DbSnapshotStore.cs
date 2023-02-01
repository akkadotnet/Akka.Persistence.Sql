using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Persistence.Snapshot;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Utility;
using Akka.Streams;
using Akka.Util;

namespace Akka.Persistence.Sql.Linq2Db.Snapshot
{
    public class Linq2DbSnapshotStore : SnapshotStore
    {
        [Obsolete(message: "Use Linq2DbPersistence.Get(ActorSystem).DefaultConfig instead")]
        public static readonly Configuration.Config DefaultConfiguration =
            ConfigurationFactory.FromResource<Linq2DbSnapshotStore>("Akka.Persistence.Sql.Linq2Db.snapshot.conf");
        
        public readonly Linq2DbPersistence Extension = Linq2DbPersistence.Get(Context.System);
        
        private readonly SnapshotConfig _snapshotConfig;
        
        private readonly ByteArraySnapshotDao _dao;

        public Linq2DbSnapshotStore(Configuration.Config snapshotConfig)
        {
            var config = snapshotConfig.WithFallback(Extension.DefaultSnapshotConfig);
            
            _snapshotConfig = new SnapshotConfig(config);
            
            _dao = new ByteArraySnapshotDao(
                connectionFactory: new AkkaPersistenceDataConnectionFactory(_snapshotConfig),
                snapshotConfig: _snapshotConfig, serialization: Context.System.Serialization,
                mat: Materializer.CreateSystemMaterializer((ExtendedActorSystem)Context.System), 
                logger: Context.GetLogger());
            
            if (_snapshotConfig.AutoInitialize)
            {
                try
                {
                    _dao.InitializeTables();
                }
                catch (Exception e)
                {
                    Context.GetLogger().Warning(e,
                        "Unable to Initialize Persistence Snapshot Table!");
                }
            }
        }
        
        protected override async Task<SelectedSnapshot> LoadAsync(string persistenceId, SnapshotSelectionCriteria criteria)
        {
            switch (criteria.MaxSequenceNr)
            {
                case long.MaxValue when criteria.MaxTimeStamp == DateTime.MaxValue:
                    return (await _dao.LatestSnapshot(persistenceId)).GetOrElse(null);
                case long.MaxValue:
                    return (await _dao.SnapshotForMaxTimestamp(persistenceId, criteria.MaxTimeStamp)).GetOrElse(null);
                default:
                {
                    return criteria.MaxTimeStamp == DateTime.MaxValue
                        ? (await _dao.SnapshotForMaxSequenceNr(
                            persistenceId: persistenceId, 
                            sequenceNr: criteria.MaxSequenceNr)).GetOrElse(null)
                        : (await _dao.SnapshotForMaxSequenceNrAndMaxTimestamp(
                            persistenceId: persistenceId,
                            sequenceNr: criteria.MaxSequenceNr,
                            timestamp: criteria.MaxTimeStamp)).GetOrElse(null);
                }
            }
        }

        protected override async Task SaveAsync(SnapshotMetadata metadata, object snapshot)
        {
            await _dao.Save(metadata, snapshot);
        }

        protected override async Task DeleteAsync(SnapshotMetadata metadata)
        {
            await _dao.Delete(metadata.PersistenceId, metadata.SequenceNr);
        }

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