using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Event;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Streams;
using Akka.Util;
using LinqToDB;

namespace Akka.Persistence.Sql.Linq2Db.Snapshot
{
    public class ByteArraySnapshotDao : ISnapshotDao
    {
        private readonly AkkaPersistenceDataConnectionFactory _connectionFactory;
        private readonly IMaterializer _mat;
        private readonly SnapshotConfig _snapshotConfig;
        private readonly Akka.Serialization.Serialization _serialization;
        private readonly ByteArraySnapshotSerializer _serializer;
        private readonly ILoggingAdapter _logger;

        // TODO: This should be converted to async
        public void InitializeTables()
        {
            using var conn = _connectionFactory.GetConnection();
            
            try
            {
                conn.CreateTable<SnapshotRow>();
            }
            catch (Exception e)
            {
                if (_snapshotConfig.TableConfig.WarnOnAutoInitializeFail)
                {
                    _logger.Warning(e,$"Could not Create Snapshot Table {_snapshotConfig.TableConfig.TableName} as requested by config.");    
                }
            }
        }
        
        public ByteArraySnapshotDao(
            AkkaPersistenceDataConnectionFactory connectionFactory,
            SnapshotConfig snapshotConfig, Akka.Serialization.Serialization serialization,
            IMaterializer mat, ILoggingAdapter logger)
        {
            _logger = logger;
            _mat = mat;
            _snapshotConfig = snapshotConfig;
            _serialization = serialization;
            _connectionFactory = connectionFactory;
            _serializer = new ByteArraySnapshotSerializer(serialization, _snapshotConfig);
        }
        
        public async Task DeleteAllSnapshots(string persistenceId)
        {
            await using var conn = _connectionFactory.GetConnection();
            
            await conn.GetTable<SnapshotRow>()
                .Where(r => r.PersistenceId == persistenceId)
                .DeleteAsync();
        }

        public async Task DeleteUpToMaxSequenceNr(string persistenceId, long maxSequenceNr)
        {
            await using var conn = _connectionFactory.GetConnection();
            
            await conn.GetTable<SnapshotRow>()
                .Where(r =>
                    r.PersistenceId == persistenceId &&
                    r.SequenceNumber <= maxSequenceNr).DeleteAsync();
        }

        public async Task DeleteUpToMaxTimestamp(string persistenceId, DateTime maxTimestamp)
        {
            await using var conn = _connectionFactory.GetConnection();
            
            await conn.GetTable<SnapshotRow>()
                .Where(r =>
                    r.PersistenceId == persistenceId &&
                    r.Created <= maxTimestamp).DeleteAsync();
        }

        public async Task DeleteUpToMaxSequenceNrAndMaxTimestamp(string persistenceId,
            long maxSequenceNr, DateTime maxTimestamp)
        {
            await using var conn = _connectionFactory.GetConnection();
            
            await conn.GetTable<SnapshotRow>()
                .Where(r =>
                    r.PersistenceId == persistenceId &&
                    r.SequenceNumber <= maxSequenceNr &&
                    r.Created <= maxTimestamp).DeleteAsync();
        }

        public async Task<Option<SelectedSnapshot>> LatestSnapshot(string persistenceId)
        {
            await using var conn = _connectionFactory.GetConnection();
            
            var row = await conn.GetTable<SnapshotRow>()
                .Where(r => r.PersistenceId == persistenceId)
                .OrderByDescending(t => t.SequenceNumber)
                .FirstOrDefaultAsync();
            
            return row != null ? _serializer.Deserialize(row).Get() : Option<SelectedSnapshot>.None;
        }

        public async Task<Option<SelectedSnapshot>> SnapshotForMaxTimestamp(string persistenceId, DateTime timestamp)
        {
            await using var conn = _connectionFactory.GetConnection();
            
            var row = await conn.GetTable<SnapshotRow>()
                .Where(r => r.PersistenceId == persistenceId && r.Created <= timestamp)
                .OrderByDescending(t => t.SequenceNumber)
                .FirstOrDefaultAsync();
            
            return row != null ? _serializer.Deserialize(row).Get() : Option<SelectedSnapshot>.None;
        }

        public async Task<Option<SelectedSnapshot>> SnapshotForMaxSequenceNr(string persistenceId, long sequenceNr)
        {
            await using var conn = _connectionFactory.GetConnection();
            
            var row = await conn.GetTable<SnapshotRow>()
                .Where(r => r.PersistenceId == persistenceId && r.SequenceNumber <= sequenceNr)
                .OrderByDescending(t => t.SequenceNumber)
                .FirstOrDefaultAsync();
            
            return row != null ? _serializer.Deserialize(row).Get() : Option<SelectedSnapshot>.None;
        }

        public async Task<Option<SelectedSnapshot>> SnapshotForMaxSequenceNrAndMaxTimestamp(string persistenceId,
            long sequenceNr, DateTime timestamp)
        {
            await using var conn = _connectionFactory.GetConnection();
            
            var row = await conn.GetTable<SnapshotRow>()
                .Where(r => r.PersistenceId == persistenceId && r.SequenceNumber <= sequenceNr && r.Created <= timestamp)
                .OrderByDescending(t => t.SequenceNumber)
                .FirstOrDefaultAsync();
            
            return row != null ? _serializer.Deserialize(row).Get() : Option<SelectedSnapshot>.None;
        }

        public async Task Delete(string persistenceId, long sequenceNr)
        {
            await using var conn = _connectionFactory.GetConnection();
            
            var _ = await conn.GetTable<SnapshotRow>()
                .Where(r => r.PersistenceId == persistenceId && r.SequenceNumber == sequenceNr)
                .DeleteAsync();
        }

        public async Task Save(SnapshotMetadata snapshotMetadata, object snapshot)
        {
            await using var conn = _connectionFactory.GetConnection();
            
            await conn.InsertOrReplaceAsync(_serializer.Serialize(snapshotMetadata, snapshot).Get());
        }
    }
}