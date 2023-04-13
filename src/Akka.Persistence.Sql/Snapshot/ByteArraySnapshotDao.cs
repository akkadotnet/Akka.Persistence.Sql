// -----------------------------------------------------------------------
//  <copyright file="ByteArraySnapshotDao.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Event;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Streams;
using Akka.Util;
using LinqToDB;

namespace Akka.Persistence.Sql.Snapshot
{
    public class ByteArraySnapshotDao : ISnapshotDao
    {
        private readonly ILoggingAdapter _logger;

        private readonly AkkaPersistenceDataConnectionFactory _connectionFactory;
        private readonly ByteArrayDateTimeSnapshotSerializer _dateTimeSerializer;
        private readonly ByteArrayLongSnapshotSerializer _longSerializer;
        private readonly SnapshotConfig _snapshotConfig;

        public ByteArraySnapshotDao(
            AkkaPersistenceDataConnectionFactory connectionFactory,
            SnapshotConfig snapshotConfig,
            Akka.Serialization.Serialization serialization,
            IMaterializer materializer,
            ILoggingAdapter logger)
        {
            _logger = logger;
            _snapshotConfig = snapshotConfig;
            _connectionFactory = connectionFactory;

            _dateTimeSerializer = new ByteArrayDateTimeSnapshotSerializer(serialization, _snapshotConfig);
            _longSerializer = new ByteArrayLongSnapshotSerializer(serialization, _snapshotConfig);
        }

        public async Task DeleteAllSnapshots(string persistenceId)
        {
            await using var connection = _connectionFactory.GetConnection();

            if (connection.UseDateTime)
            {
                await connection
                    .GetTable<DateTimeSnapshotRow>()
                    .Where(r => r.PersistenceId == persistenceId)
                    .DeleteAsync();
            }
            else
            {
                await connection
                    .GetTable<LongSnapshotRow>()
                    .Where(r => r.PersistenceId == persistenceId)
                    .DeleteAsync();
            }
        }

        public async Task DeleteUpToMaxSequenceNr(string persistenceId, long maxSequenceNr)
        {
            await using var connection = _connectionFactory.GetConnection();

            if (connection.UseDateTime)
            {
                await connection
                    .GetTable<DateTimeSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.SequenceNumber <= maxSequenceNr)
                    .DeleteAsync();
            }
            else
            {
                await connection
                    .GetTable<LongSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.SequenceNumber <= maxSequenceNr)
                    .DeleteAsync();
            }
        }

        public async Task DeleteUpToMaxTimestamp(string persistenceId, DateTime maxTimestamp)
        {
            await using var connection = _connectionFactory.GetConnection();

            if (connection.UseDateTime)
            {
                await connection
                    .GetTable<DateTimeSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.Created <= maxTimestamp)
                    .DeleteAsync();
            }
            else
            {
                await connection
                    .GetTable<LongSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.Created <= maxTimestamp.Ticks)
                    .DeleteAsync();
            }
        }

        public async Task DeleteUpToMaxSequenceNrAndMaxTimestamp(
            string persistenceId,
            long maxSequenceNr,
            DateTime maxTimestamp)
        {
            await using var connection = _connectionFactory.GetConnection();

            if (connection.UseDateTime)
            {
                await connection
                    .GetTable<DateTimeSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.SequenceNumber <= maxSequenceNr &&
                        r.Created <= maxTimestamp)
                    .DeleteAsync();
            }
            else
            {
                await connection
                    .GetTable<LongSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.SequenceNumber <= maxSequenceNr &&
                        r.Created <= maxTimestamp.Ticks)
                    .DeleteAsync();
            }
        }

        public async Task<Option<SelectedSnapshot>> LatestSnapshot(string persistenceId)
        {
            await using var connection = _connectionFactory.GetConnection();

            if (connection.UseDateTime)
            {
                var row = await connection
                    .GetTable<DateTimeSnapshotRow>()
                    .Where(r => r.PersistenceId == persistenceId)
                    .OrderByDescending(t => t.SequenceNumber)
                    .FirstOrDefaultAsync();

                return row != null
                    ? _dateTimeSerializer.Deserialize(row).Get()
                    : Option<SelectedSnapshot>.None;
            }
            else
            {
                var row = await connection
                    .GetTable<LongSnapshotRow>()
                    .Where(r => r.PersistenceId == persistenceId)
                    .OrderByDescending(t => t.SequenceNumber)
                    .FirstOrDefaultAsync();

                return row != null
                    ? _longSerializer.Deserialize(row).Get()
                    : Option<SelectedSnapshot>.None;
            }
        }

        public async Task<Option<SelectedSnapshot>> SnapshotForMaxTimestamp(string persistenceId, DateTime timestamp)
        {
            await using var connection = _connectionFactory.GetConnection();

            if (connection.UseDateTime)
            {
                var row = await connection
                    .GetTable<DateTimeSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.Created <= timestamp)
                    .OrderByDescending(t => t.SequenceNumber)
                    .FirstOrDefaultAsync();

                return row != null
                    ? _dateTimeSerializer.Deserialize(row).Get()
                    : Option<SelectedSnapshot>.None;
            }
            else
            {
                var row = await connection
                    .GetTable<LongSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.Created <= timestamp.Ticks)
                    .OrderByDescending(t => t.SequenceNumber)
                    .FirstOrDefaultAsync();

                return row != null
                    ? _longSerializer.Deserialize(row).Get()
                    : Option<SelectedSnapshot>.None;
            }
        }

        public async Task<Option<SelectedSnapshot>> SnapshotForMaxSequenceNr(string persistenceId, long sequenceNr)
        {
            await using var connection = _connectionFactory.GetConnection();

            if (connection.UseDateTime)
            {
                var row = await connection
                    .GetTable<DateTimeSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.SequenceNumber <= sequenceNr)
                    .OrderByDescending(t => t.SequenceNumber)
                    .FirstOrDefaultAsync();

                return row != null
                    ? _dateTimeSerializer.Deserialize(row).Get()
                    : Option<SelectedSnapshot>.None;
            }
            else
            {
                var row = await connection
                    .GetTable<LongSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.SequenceNumber <= sequenceNr)
                    .OrderByDescending(t => t.SequenceNumber)
                    .FirstOrDefaultAsync();

                return row != null
                    ? _longSerializer.Deserialize(row).Get()
                    : Option<SelectedSnapshot>.None;
            }
        }

        public async Task<Option<SelectedSnapshot>> SnapshotForMaxSequenceNrAndMaxTimestamp(
            string persistenceId,
            long sequenceNr,
            DateTime timestamp)
        {
            await using var connection = _connectionFactory.GetConnection();

            if (connection.UseDateTime)
            {
                var row = await connection
                    .GetTable<DateTimeSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.SequenceNumber <= sequenceNr &&
                        r.Created <= timestamp)
                    .OrderByDescending(t => t.SequenceNumber)
                    .FirstOrDefaultAsync();

                return row != null
                    ? _dateTimeSerializer.Deserialize(row).Get()
                    : Option<SelectedSnapshot>.None;
            }
            else
            {
                var row = await connection
                    .GetTable<LongSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.SequenceNumber <= sequenceNr &&
                        r.Created <= timestamp.Ticks)
                    .OrderByDescending(t => t.SequenceNumber)
                    .FirstOrDefaultAsync();

                return row != null
                    ? _longSerializer.Deserialize(row).Get()
                    : Option<SelectedSnapshot>.None;
            }
        }

        public async Task Delete(string persistenceId, long sequenceNr)
        {
            await using var connection = _connectionFactory.GetConnection();

            if (connection.UseDateTime)
            {
                await connection
                    .GetTable<DateTimeSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.SequenceNumber == sequenceNr)
                    .DeleteAsync();
            }
            else
            {
                await connection
                    .GetTable<LongSnapshotRow>()
                    .Where(r =>
                        r.PersistenceId == persistenceId &&
                        r.SequenceNumber == sequenceNr)
                    .DeleteAsync();
            }
        }

        public async Task Save(SnapshotMetadata snapshotMetadata, object snapshot)
        {
            await using var connection = _connectionFactory.GetConnection();

            if (connection.UseDateTime)
            {
                await connection
                    .InsertOrReplaceAsync(
                        _dateTimeSerializer
                            .Serialize(snapshotMetadata, snapshot)
                            .Get());
            }
            else
            {
                await connection
                    .InsertOrReplaceAsync(
                        _longSerializer
                            .Serialize(snapshotMetadata, snapshot)
                            .Get());
            }
        }

        public async Task InitializeTables()
        {
            await using var connection = _connectionFactory.GetConnection();
            var footer = _snapshotConfig.GenerateSnapshotFooter();
            if (connection.UseDateTime)
            {
                await connection.CreateTableAsync<DateTimeSnapshotRow>(TableOptions.CreateIfNotExists, footer);
            }
            else
            {
                await connection.CreateTableAsync<LongSnapshotRow>(TableOptions.CreateIfNotExists, footer);
            }
        }
    }
}
