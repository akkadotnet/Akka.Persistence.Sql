// -----------------------------------------------------------------------
//  <copyright file="ByteArraySnapshotDao.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Akka.Event;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Extensions;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;
using LinqToDB;
using LinqToDB.Tools;

namespace Akka.Persistence.Sql.Snapshot
{
    public static class SubFlowExtensions
    {
        public static Source<TOut, TMat> MergeSubStreamsAsSource<TOut, TMat,
            TClosed>(this SubFlow<TOut, TMat, TClosed> subFlow)
        {
            return (Source<TOut,TMat>)(subFlow.MergeSubstreams());
        }
    }

    public class LatestSnapRequestEntry
    {
        public LatestSnapRequestEntry(string persistenceId, CancellationToken cancellationToken)
        {
            PersistenceId = persistenceId;
            CTS = cancellationToken;
            TCS = new TaskCompletionSource<Option<SelectedSnapshot>>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public readonly string PersistenceId;
        public readonly TaskCompletionSource<Option<SelectedSnapshot>> TCS;
        public readonly CancellationToken CTS;
    }

    public readonly record struct SnapshotReadGroup
    {
        public SnapshotReadGroup(QueryLatestSnapSet a, List<LongSnapshotRow> b, Exception? err)
        {
            this.a = a;
            this.b = b;
            this.err = err;
        }
        public QueryLatestSnapSet a { get; }
        public List<LongSnapshotRow> b { get; }
        public Exception? err { get; }
        public void Deconstruct(out QueryLatestSnapSet a, out List<LongSnapshotRow> b, out Exception? err)
        {
            a = this.a;
            b = this.b;
            err = this.err;
        }
    }

    public class QueryLatestSnapSet
    {
        public readonly Dictionary<string, List<LatestSnapRequestEntry>> Entries = new();

        public void Add(LatestSnapRequestEntry entry)
        {
            if (Entries.TryGetValue(entry.PersistenceId, out var item) == false)
            {
                item = Entries[entry.PersistenceId] = new List<LatestSnapRequestEntry>();
            }
            item.Add(entry);
        }
    }

    public class ByteArraySnapshotDao : ISnapshotDao
    {
        private readonly AkkaPersistenceDataConnectionFactory _connectionFactory;
        private readonly ByteArrayDateTimeSnapshotSerializer _dateTimeSerializer;
        private readonly ILoggingAdapter _logger;
        private readonly ByteArrayLongSnapshotSerializer _longSerializer;
        private readonly IsolationLevel _readIsolationLevel;
        private readonly CancellationTokenSource _shutdownCts;
        private readonly SnapshotConfig _snapshotConfig;
        private readonly IsolationLevel _writeIsolationLevel;
        private readonly Channel<LatestSnapRequestEntry> _pendingLatestChannel;
        private readonly Task<Done> _latestSnapStream;
        private readonly int _maxSubStreamsForReads;
        private readonly int _maxBatchPerSubStreamRead;

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

            _writeIsolationLevel = snapshotConfig.WriteIsolationLevel;
            _readIsolationLevel = snapshotConfig.ReadIsolationLevel;

            _shutdownCts = new CancellationTokenSource();
            _pendingLatestChannel = Channel.CreateUnbounded<LatestSnapRequestEntry>();
            _maxSubStreamsForReads =  _snapshotConfig.MaxSubStreamsForReads;
            _maxBatchPerSubStreamRead = _snapshotConfig.MaxBatchPerSubStreamRead;
            _latestSnapStream = Source.ChannelReader(_pendingLatestChannel.Reader)
                .GroupBy(_maxSubStreamsForReads, a=> a.PersistenceId.GetHashCode()% _maxSubStreamsForReads)
                .BatchWeighted(
                    _maxBatchPerSubStreamRead,
                    a => 1,
                    e =>
                    {
                        var a = new QueryLatestSnapSet();
                        a.Add(e);
                        return a;
                    },
                    (a, e) =>
                    {
                        a.Add(e);
                        return a;
                    })
                .SelectAsync(1,
                    async a =>
                    {

                        using (var connection = _connectionFactory.GetConnection())
                        {
                            if (connection.UseDateTime)
                            {
                                //TODO: Consolidate/fixup different rowtype issues.
                                try
                                {
                                    var set = await connection.GetTable<DateTimeSnapshotRow>()
                                        .Where(r => r.PersistenceId.In(a.Entries.Keys))
                                        .Select(
                                            r => new
                                            {
                                                Created = r.Created,
                                                PersistenceId = r.PersistenceId,
                                                SequenceNumber = r.SequenceNumber,
                                                Manifest = r.Manifest,
                                                Payload = r.Payload,
                                                SerializerId = r.SerializerId,
                                                RowNum = LinqToDB.Sql.Ext.Rank().Over().PartitionBy(r.PersistenceId).OrderByDesc(r.SequenceNumber).ToValue()
                                            })
                                        .Where(r => r.RowNum == 1)
                                        .Select(
                                            r => new LongSnapshotRow()
                                            {
                                                Created = r.Created.Ticks,
                                                PersistenceId = r.PersistenceId,
                                                SequenceNumber = r.SequenceNumber,
                                                Manifest = r.Manifest,
                                                Payload = r.Payload,
                                                SerializerId = r.SerializerId,
                                            }).ToListAsync();
                                    return new SnapshotReadGroup(a, set, err: (Exception?)null);
                                }
                                catch (Exception ex)
                                {
                                    return new (a, null, err: ex);
                                }
                            }
                            else
                            {
                                try
                                {
                                    var set = await connection.GetTable<LongSnapshotRow>()
                                        .Where(r => r.PersistenceId.In(a.Entries.Keys))
                                        .Select(
                                            r => new
                                            {
                                                Created = r.Created,
                                                PersistenceId = r.PersistenceId,
                                                SequenceNumber = r.SequenceNumber,
                                                Manifest = r.Manifest,
                                                Payload = r.Payload,
                                                SerializerId = r.SerializerId,
                                                LatestRowNum = LinqToDB.Sql.Ext.Rank().Over().PartitionBy(r.PersistenceId).OrderByDesc(r.SequenceNumber).ToValue()
                                            })
                                        .Where(r => r.LatestRowNum == 1)
                                        .Select(
                                            r => new LongSnapshotRow()
                                            {
                                                Created = r.Created,
                                                PersistenceId = r.PersistenceId,
                                                SequenceNumber = r.SequenceNumber,
                                                Manifest = r.Manifest,
                                                Payload = r.Payload,
                                                SerializerId = r.SerializerId,
                                            }).ToListAsync();
                                    return new (a, set, err: (Exception?)null);
                                }
                                catch (Exception ex)
                                {
                                    return new (a, null, err: ex);
                                }
                            }
                        }
                    }).SelectAsync(1,
                    async (ab) =>
                    {
                        var (a, b, c) = ab;
                        if (c != null)
                        {
                            foreach (var taskCompletionSourcese in a!.Entries.Values.ToList())
                            {
                                foreach (var taskCompletionSource in taskCompletionSourcese)
                                {
                                    if (taskCompletionSource.CTS.IsCancellationRequested)
                                    {
                                        taskCompletionSource.TCS.TrySetCanceled(taskCompletionSource.CTS);
                                    }
                                    else
                                    {
                                     
                                        taskCompletionSource.TCS.TrySetException(c);   
                                    }
                                }
                            }
                        }
                        else
                        {
                            //TODO: Pool this set:
                            var tempSet = new List<string>();
                            if (b.Count == 0)
                            {
                                foreach (var keyValuePair in a.Entries)
                                {
                                    foreach (var taskCompletionSource in keyValuePair.Value)
                                    {
                                        if (taskCompletionSource.CTS.IsCancellationRequested)
                                        {
                                            taskCompletionSource.TCS.TrySetCanceled(taskCompletionSource.CTS);
                                        }
                                        else
                                        {
                                            taskCompletionSource.TCS.TrySetResult(Option<SelectedSnapshot>.None);
                                        }
                                    }
                                }
                            }
                            foreach (var result in b)
                            {
                                if (a.Entries.TryGetValue(result.PersistenceId, out var toSet))
                                {
                                    try
                                    {
                                        var res = _longSerializer.Deserialize(result);
                                        if (res.IsSuccess)
                                        {
                                            foreach (var taskCompletionSource in toSet)
                                            {
                                                if (taskCompletionSource.CTS.IsCancellationRequested)
                                                {
                                                    taskCompletionSource.TCS.TrySetCanceled(taskCompletionSource.CTS);
                                                }
                                                else
                                                {
                                                    taskCompletionSource.TCS.TrySetResult(res.Success);
                                                }
                                            }
                                        }
                                        else
                                        {
                                            foreach (var taskCompletionSource in toSet)
                                            {
                                                if (taskCompletionSource.CTS.IsCancellationRequested)
                                                {
                                                    taskCompletionSource.TCS.TrySetCanceled(taskCompletionSource.CTS);
                                                }
                                                else
                                                {
                                                    taskCompletionSource.TCS.TrySetException(res.Failure.Value);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        foreach (var taskCompletionSource in toSet)
                                        {
                                            taskCompletionSource.TCS.TrySetException(e);
                                        }
                                    }
                                }
                                else
                                {
                                    tempSet.Add(result.PersistenceId);
                                }

                                foreach (var se in tempSet)
                                {
                                    if (a.Entries.TryGetValue(se, out var setNo))
                                    {
                                        foreach (var taskCompletionSource in setNo)
                                        {
                                            taskCompletionSource.TCS.TrySetResult(Option<SelectedSnapshot>.None);
                                        }
                                    }
                                }
                            }
                        }

                        return Done.Instance;
                    }).MergeSubStreamsAsSource().RunWith(Sink.Ignore<Done>(), materializer);
        }

        public async Task DeleteAllSnapshotsAsync(
            string persistenceId,
            CancellationToken cancellationToken = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
            await _connectionFactory.ExecuteWithTransactionAsync(
                _writeIsolationLevel,
                cts.Token,
                async (connection, token) =>
                {
                    if (connection.UseDateTime)
                    {
                        await connection
                            .GetTable<DateTimeSnapshotRow>()
                            .Where(r => r.PersistenceId == persistenceId)
                            .DeleteAsync(token);
                    }
                    else
                    {
                        await connection
                            .GetTable<LongSnapshotRow>()
                            .Where(r => r.PersistenceId == persistenceId)
                            .DeleteAsync(token);
                    }
                });
        }

        public async Task DeleteUpToMaxSequenceNrAsync(
            string persistenceId,
            long maxSequenceNr,
            CancellationToken cancellationToken = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
            await _connectionFactory.ExecuteWithTransactionAsync(
                _writeIsolationLevel,
                cts.Token,
                async (connection, token) =>
                {
                    if (connection.UseDateTime)
                    {
                        await connection
                            .GetTable<DateTimeSnapshotRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.SequenceNumber <= maxSequenceNr)
                            .DeleteAsync(token);
                    }
                    else
                    {
                        await connection
                            .GetTable<LongSnapshotRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.SequenceNumber <= maxSequenceNr)
                            .DeleteAsync(token);
                    }
                });
        }

        public async Task DeleteUpToMaxTimestampAsync(
            string persistenceId,
            DateTime maxTimestamp,
            CancellationToken cancellationToken = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
            await _connectionFactory.ExecuteWithTransactionAsync(
                _writeIsolationLevel,
                cts.Token,
                async (connection, token) =>
                {
                    if (connection.UseDateTime)
                    {
                        await connection
                            .GetTable<DateTimeSnapshotRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.Created <= maxTimestamp)
                            .DeleteAsync(token);
                    }
                    else
                    {
                        await connection
                            .GetTable<LongSnapshotRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.Created <= maxTimestamp.Ticks)
                            .DeleteAsync(token);
                    }
                });
        }

        public async Task DeleteUpToMaxSequenceNrAndMaxTimestampAsync(
            string persistenceId,
            long maxSequenceNr,
            DateTime maxTimestamp,
            CancellationToken cancellationToken = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
            await _connectionFactory.ExecuteWithTransactionAsync(
                _writeIsolationLevel,
                cts.Token,
                async (connection, token) =>
                {
                    if (connection.UseDateTime)
                    {
                        await connection
                            .GetTable<DateTimeSnapshotRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.SequenceNumber <= maxSequenceNr &&
                                    r.Created <= maxTimestamp)
                            .DeleteAsync(token);
                    }
                    else
                    {
                        await connection
                            .GetTable<LongSnapshotRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.SequenceNumber <= maxSequenceNr &&
                                    r.Created <= maxTimestamp.Ticks)
                            .DeleteAsync(token);
                    }
                });
        }

        public Task<Option<SelectedSnapshot>> LatestSnapshotAsync(
            string persistenceId,
            CancellationToken cancellationToken = default)
        {
            var req = new LatestSnapRequestEntry(persistenceId, cancellationToken);
            if (_pendingLatestChannel.Writer.TryWrite(req))
            {
                return req.TCS.Task;
            }
            else
            {
                return Task.FromException<Option<SelectedSnapshot>>(new Exception("Queue is closed, System may be shutting down!"));
            }
            //var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
            //return await _connectionFactory.ExecuteWithTransactionAsync(
            //    _readIsolationLevel,
            //    cts.Token,
            //    async (connection, token) =>
            //    {
            //        if (connection.UseDateTime)
            //        {
            //            var row = await connection
            //                .GetTable<DateTimeSnapshotRow>()
            //                .Where(r => r.PersistenceId == persistenceId)
            //                .OrderByDescending(t => t.SequenceNumber)
            //                .FirstOrDefaultAsync(token);
            //
            //            return row != null
            //                ? _dateTimeSerializer.Deserialize(row).Get()
            //                : Option<SelectedSnapshot>.None;
            //        }
            //        else
            //        {
            //            var row = await connection
            //                .GetTable<LongSnapshotRow>()
            //                .Where(r => r.PersistenceId == persistenceId)
            //                .OrderByDescending(t => t.SequenceNumber)
            //                .FirstOrDefaultAsync(token);
            //
            //            return row != null
            //                ? _longSerializer.Deserialize(row).Get()
            //                : Option<SelectedSnapshot>.None;
            //        }
            //    });
        }

        public async Task<Option<SelectedSnapshot>> SnapshotForMaxTimestampAsync(
            string persistenceId,
            DateTime timestamp,
            CancellationToken cancellationToken = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
            return await _connectionFactory.ExecuteWithTransactionAsync(
                _readIsolationLevel,
                cts.Token,
                async (connection, token) =>
                {
                    if (connection.UseDateTime)
                    {
                        var row = await connection
                            .GetTable<DateTimeSnapshotRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.Created <= timestamp)
                            .OrderByDescending(t => t.SequenceNumber)
                            .FirstOrDefaultAsync(token);

                        return row != null
                            ? _dateTimeSerializer.Deserialize(row).Get()
                            : Option<SelectedSnapshot>.None;
                    }
                    else
                    {
                        var row = await connection
                            .GetTable<LongSnapshotRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.Created <= timestamp.Ticks)
                            .OrderByDescending(t => t.SequenceNumber)
                            .FirstOrDefaultAsync(token);

                        return row != null
                            ? _longSerializer.Deserialize(row).Get()
                            : Option<SelectedSnapshot>.None;
                    }
                });
        }

        public async Task<Option<SelectedSnapshot>> SnapshotForMaxSequenceNrAsync(
            string persistenceId,
            long sequenceNr,
            CancellationToken cancellationToken = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
            return await _connectionFactory.ExecuteWithTransactionAsync(
                _readIsolationLevel,
                cts.Token,
                async (connection, token) =>
                {
                    if (connection.UseDateTime)
                    {
                        var row = await connection
                            .GetTable<DateTimeSnapshotRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.SequenceNumber <= sequenceNr)
                            .OrderByDescending(t => t.SequenceNumber)
                            .FirstOrDefaultAsync(token);

                        return row != null
                            ? _dateTimeSerializer.Deserialize(row).Get()
                            : Option<SelectedSnapshot>.None;
                    }
                    else
                    {
                        var row = await connection
                            .GetTable<LongSnapshotRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.SequenceNumber <= sequenceNr)
                            .OrderByDescending(t => t.SequenceNumber)
                            .FirstOrDefaultAsync(token);

                        return row != null
                            ? _longSerializer.Deserialize(row).Get()
                            : Option<SelectedSnapshot>.None;
                    }
                });
        }

        public async Task<Option<SelectedSnapshot>> SnapshotForMaxSequenceNrAndMaxTimestampAsync(
            string persistenceId,
            long sequenceNr,
            DateTime timestamp,
            CancellationToken cancellationToken = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
            return await _connectionFactory.ExecuteWithTransactionAsync(
                _readIsolationLevel,
                cts.Token,
                async (connection, token) =>
                {
                    if (connection.UseDateTime)
                    {
                        var row = await connection
                            .GetTable<DateTimeSnapshotRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.SequenceNumber <= sequenceNr &&
                                    r.Created <= timestamp)
                            .OrderByDescending(t => t.SequenceNumber)
                            .FirstOrDefaultAsync(token);

                        return row != null
                            ? _dateTimeSerializer.Deserialize(row).Get()
                            : Option<SelectedSnapshot>.None;
                    }
                    else
                    {
                        var row = await connection
                            .GetTable<LongSnapshotRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.SequenceNumber <= sequenceNr &&
                                    r.Created <= timestamp.Ticks)
                            .OrderByDescending(t => t.SequenceNumber)
                            .FirstOrDefaultAsync(token);

                        return row != null
                            ? _longSerializer.Deserialize(row).Get()
                            : Option<SelectedSnapshot>.None;
                    }
                });
        }

        public async Task DeleteAsync(
            string persistenceId,
            long sequenceNr,
            DateTime timestamp,
            CancellationToken cancellationToken = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
            await _connectionFactory.ExecuteWithTransactionAsync(
                _writeIsolationLevel,
                cts.Token,
                async (connection, token) =>
                {
                    if (connection.UseDateTime)
                    {
                        var query = connection.GetTable<DateTimeSnapshotRow>()
                                .Where(
                                    r =>
                                        r.PersistenceId == persistenceId &&
                                        r.SequenceNumber == sequenceNr);
                        
                        if (timestamp > DateTime.MinValue)
                            query = query.Where(r => r.Created <= timestamp);
                        
                        await query
                            .DeleteAsync(token);
                    }
                    else
                    {
                        var query = connection
                            .GetTable<LongSnapshotRow>()
                            .Where(
                                r =>
                                    r.PersistenceId == persistenceId &&
                                    r.SequenceNumber == sequenceNr);
                        
                        if (timestamp > DateTime.MinValue)
                            query = query.Where(r => r.Created <= timestamp.Ticks);
                        
                        await query
                            .DeleteAsync(token);
                    }
                });
        }

        public async Task SaveAsync(
            SnapshotMetadata snapshotMetadata,
            object snapshot,
            CancellationToken cancellationToken = default)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _shutdownCts.Token);
            await _connectionFactory.ExecuteWithTransactionAsync(
                _writeIsolationLevel,
                cts.Token,
                async (connection, token) =>
                {
                    if (connection.UseDateTime)
                    {
                        await connection
                            .InsertOrReplaceAsync(
                                _dateTimeSerializer
                                    .Serialize(snapshotMetadata, snapshot)
                                    .Get(),
                                token);
                    }
                    else
                    {
                        await connection
                            .InsertOrReplaceAsync(
                                _longSerializer
                                    .Serialize(snapshotMetadata, snapshot)
                                    .Get(),
                                token);
                    }
                });
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
