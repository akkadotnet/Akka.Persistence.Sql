using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Serialization;
using Akka.Streams;
using Akka.Streams.Dsl;
using LanguageExt;
using LinqToDB;using LinqToDB.Common;
using LinqToDB.Data;
using static LanguageExt.Prelude;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Dao
{
    public class SequentialUuidGenerator
    {
        private long _counter = DateTime.UtcNow.Ticks;

        /// <summary>
        ///     Gets a value to be assigned to a property.
        /// </summary>
        /// <param name="entry">The change tracking entry of the entity for which the value is being generated.</param>
        /// <returns>The value to be assigned to a property.</returns>
        public Guid Next()
        {
            var guidBytes = Guid.NewGuid().ToByteArray();
            var counterBytes = BitConverter.GetBytes(Interlocked.Increment(ref _counter));

            if (!BitConverter.IsLittleEndian)
            {
                System.Array.Reverse(counterBytes);
            }

            guidBytes[08] = counterBytes[1];
            guidBytes[09] = counterBytes[0];
            guidBytes[10] = counterBytes[7];
            guidBytes[11] = counterBytes[6];
            guidBytes[12] = counterBytes[5];
            guidBytes[13] = counterBytes[4];
            guidBytes[14] = counterBytes[3];
            guidBytes[15] = counterBytes[2];

            return new Guid(guidBytes);
        }
    }
    
    public abstract class BaseByteArrayJournalDao :
        BaseJournalDaoWithReadMessages,
        IJournalDaoWithUpdates
    {
        public readonly ISourceQueueWithComplete<WriteQueueEntry> WriteQueue;
        protected readonly JournalConfig JournalConfig;
        protected readonly FlowPersistentReprSerializer<JournalRow> Serializer;

        private readonly Lazy<object> _logWarnAboutLogicalDeletionDeprecation =
            new(() => new object(), LazyThreadSafetyMode.None);

        public readonly bool LogicalDelete;
        protected readonly ILoggingAdapter Logger;
        private readonly Flow<JournalRow, Util.Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed> _deserializeFlow;
        private readonly Flow<JournalRow, Util.Try<ReplayCompletion>, NotUsed> _deserializeFlowMapped;
        private readonly SequentialUuidGenerator _uuidGen;
        
        protected BaseByteArrayJournalDao(
            IAdvancedScheduler scheduler,
            IMaterializer materializer,
            AkkaPersistenceDataConnectionFactory connectionFactory,
            JournalConfig config, 
            ByteArrayJournalSerializer serializer, 
            ILoggingAdapter logger) 
            : base(scheduler, materializer, connectionFactory)
        {
            Logger = logger;
            JournalConfig = config;
            LogicalDelete = JournalConfig.DaoConfig.LogicalDelete;
            Serializer = serializer;
            _deserializeFlow = Serializer.DeserializeFlow();
            _deserializeFlowMapped = Serializer.DeserializeFlow().Select(MessageWithBatchMapper());
            _uuidGen = new SequentialUuidGenerator();
            
            //Due to C# rules we have to initialize WriteQueue here
            //Keeping it here vs init function prevents accidental moving of init
            //to where variables aren't set yet.
            WriteQueue = Source
                .Queue<WriteQueueEntry>(JournalConfig.DaoConfig.BufferSize, OverflowStrategy.DropNew)
                .BatchWeighted(
                    JournalConfig.DaoConfig.BatchSize,
                    cf => cf.Rows.Count,
                    r => new WriteQueueSet(ImmutableList.Create(new[] {r.Tcs}), r.Rows),
                    (oldRows, newRows) => 
                        new WriteQueueSet(
                            oldRows.Tcs.Add(newRows.Tcs),
                            oldRows.Rows.Concat(newRows.Rows)))
                .SelectAsync(
                    JournalConfig.DaoConfig.Parallelism,
                    async promisesAndRows =>
                    {
                        try
                        {
                            await WriteJournalRows(promisesAndRows.Rows);
                            foreach (var taskCompletionSource in promisesAndRows.Tcs)
                            {
                                taskCompletionSource.TrySetResult(NotUsed.Instance);
                            }
                        }
                        catch (Exception e)
                        {
                            foreach (var taskCompletionSource in promisesAndRows.Tcs)
                            {
                                taskCompletionSource.TrySetException(e);
                            }
                        }

                        return NotUsed.Instance;
                    }).ToMaterialized(
                    Sink.Ignore<NotUsed>(), Keep.Left).Run(Materializer);
        }

        private async Task QueueWriteJournalRows(Seq<JournalRow> xs)
        {
            var promise = new TaskCompletionSource<NotUsed>(TaskCreationOptions.RunContinuationsAsynchronously);
            
            //Send promise and rows into queue. If the Queue takes it,
            //It will write the Promise state when finished writing (or failing)
            var result = await WriteQueue.OfferAsync(new WriteQueueEntry(promise, xs));
            
            switch (result)
            {
                case QueueOfferResult.Enqueued _:
                    break;
                
                case QueueOfferResult.Failure f:
                    promise.TrySetException(new Exception("Failed to write journal row batch", f.Cause));
                    break;
                
                case QueueOfferResult.Dropped _:
                    promise.TrySetException(new Exception(
                        $"Failed to enqueue journal row batch write, the queue buffer was full ({JournalConfig.DaoConfig.BufferSize} elements)"));
                    break;
                
                case QueueOfferResult.QueueClosed _:
                    promise.TrySetException(new Exception(
                        "Failed to enqueue journal row batch write, the queue was closed."));
                    break;
            }

            await promise.Task;
        }

        private async Task WriteJournalRows(Seq<JournalRow> xs)
        {
            //hot path:
            //If we only have one row, penalty for BulkCopy
            //Isn't worth it due to insert caching/transaction/etc.
            switch (xs.Count)
            {
                case 0:
                    break;
                case 1:
                    await InsertSingle(xs);
                    break;
                default:
                    await InsertMultiple(xs);
                    break;
            }
        }

        private async Task InsertSingle(Seq<JournalRow> xs)
        {
            if ((JournalConfig.TableConfig.TagWriteMode & TagWriteMode.TagTable)!=0 && xs.Head.TagArr.Length>0)
            {
                //Lazy fallback; do the InsertMultiple call here and leave it at that.
                await InsertMultiple(xs);
            }
            else
            {
                //If we are writing a single row,
                //we don't need to worry about transactions.
                await using var db = ConnectionFactory.GetConnection();
                await db.InsertAsync(xs.Head);
            }
        }

        private async Task InsertWithOrderingAndBulkInsertTags(DataConnection dc, Seq<JournalRow> xs)
        {
            var tagsToInsert = new List<JournalTagRow>(xs.Count);
            foreach (var journalRow in xs)
            {
                var dbid = await dc.InsertWithInt64IdentityAsync(journalRow);
                foreach (var s1 in journalRow.TagArr)
                {
                    tagsToInsert.Add(new JournalTagRow{ JournalOrderingId = dbid, TagValue = s1 });
                }
            }
            await dc.GetTable<JournalTagRow>().BulkCopyAsync(new BulkCopyOptions
                {
                    BulkCopyType = BulkCopyType.MultipleRows,
                    UseParameters = JournalConfig.DaoConfig.PreferParametersOnMultiRowInsert,
                    MaxBatchSize = JournalConfig.DaoConfig.DbRoundTripTagBatchSize
                }, tagsToInsert);
        }
        
        private async Task BulkInsertNoTagTableTags(DataConnection dc, Seq<JournalRow> xs)
        {
            await dc.GetTable<JournalRow>().BulkCopyAsync(new BulkCopyOptions
                {
                    BulkCopyType = xs.Count > JournalConfig.DaoConfig.MaxRowByRowSize ? BulkCopyType.Default : BulkCopyType.MultipleRows,
                    UseParameters = JournalConfig.DaoConfig.PreferParametersOnMultiRowInsert,
                    MaxBatchSize = JournalConfig.DaoConfig.DbRoundTripBatchSize
                }, xs);
        }
        
        private async Task InsertMultiple(Seq<JournalRow> xs)
        {
            if ((JournalConfig.TableConfig.TagWriteMode & TagWriteMode.TagTable) !=0)
            {
                if (JournalConfig.TableConfig.TagTableMode == TagTableMode.OrderingId)
                {
                    await HandleTagTableInsert(xs);
                }
                else
                {
                    await HandleTagTableUuidInsert(xs);
                }
            }
            else
            {
                await HandleDefaultInsert(xs);
            }
        }
        
        private async Task HandleTagTableUuidInsert(Seq<JournalRow> xs)
        {
            var tagWrites = new List<JournalTagRow>();
            foreach (var journalRow in xs)
            {
                if (journalRow.TagArr?.Length > 0)
                {
                    var uid = NextUuid();
                    journalRow.WriteUuid = uid;
                    foreach (var s1 in journalRow.TagArr)
                    {
                        tagWrites.Add(new JournalTagRow { WriteUuid = uid, TagValue = s1 });
                    }
                }
            }

            await using var ctx = ConnectionFactory.GetConnection();
            await using var tx = await ctx.BeginTransactionAsync();
            try
            {
                await ctx.BulkCopyAsync(new BulkCopyOptions
                {
                    TableName = JournalConfig.TableConfig.TableName,
                    MaxBatchSize = JournalConfig.DaoConfig.DbRoundTripBatchSize
                },xs);
                
                if (tagWrites.Count > 0)
                {
                    await ctx.BulkCopyAsync(new BulkCopyOptions
                    {
                        TableName = JournalConfig.TableConfig.TagTableName,
                        MaxBatchSize = JournalConfig.DaoConfig.DbRoundTripTagBatchSize,
                        UseParameters = JournalConfig.DaoConfig.PreferParametersOnMultiRowInsert
                    }, tagWrites);    
                }
                await ctx.CommitTransactionAsync();
            }
            catch (Exception e1)
            {
                try
                {
                    await ctx.RollbackTransactionAsync();
                }
                catch (Exception e2)
                {
                    throw new AggregateException(e2, e1);
                }
                
                throw;
            }
        }

        private Guid NextUuid()
        {
            return _uuidGen.Next();
        }
        
        private async Task HandleDefaultInsert(Seq<JournalRow> xs)
        {
            await using var db = ConnectionFactory.GetConnection();
            await using var tx = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                await BulkInsertNoTagTableTags(db, xs);
                await db.CommitTransactionAsync();
            }
            catch (Exception e1)
            {
                try
                {
                    await db.RollbackTransactionAsync();
                }
                catch (Exception e2)
                {
                    throw new AggregateException(e2, e1);
                }
                
                throw;
            }
        }

        private async Task HandleTagTableInsert(Seq<JournalRow> xs)
        {
            await using var db = ConnectionFactory.GetConnection();
            await using var tx = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted);
            try
            {
                await ConsumeSequenceForTagInsert(xs, db);
                await db.CommitTransactionAsync();
            }
            catch (Exception e1)
            {
                try
                {
                    await db.RollbackTransactionAsync();
                }
                catch (Exception e2)
                {
                    throw new AggregateException(e2, e1);
                }
                
                throw;
            }
        }

        private async Task ConsumeSequenceForTagInsert(Seq<JournalRow> xs, DataConnection db)
        {
            var tail = xs;
            while (tail.Count > 0)
            {
                (var noTags, tail) = tail.Span(r => r.TagArr.Length == 0);
                if (noTags.Count > 0)
                {
                    await BulkInsertNoTagTableTags(db, noTags);
                }

                (var hasTags, tail) = tail.Span(r => r.TagArr.Length > 0);
                if (hasTags.Count > 0)
                {
                    await InsertWithOrderingAndBulkInsertTags(db, hasTags);
                }
            }
        }
        
        //By using a custom flatten here, we avoid an Enumerable/LINQ allocation
        //And are able to have a little more control over default capacity of array.
        private static List<JournalRow> FlattenListOfListsToList(List<Util.Try<JournalRow[]>> source) 
        {
            var rows = new List<JournalRow>(source.Count > 4 ? source.Count:4);
            foreach (var t in source)
            {
                var item = t.Success.Value;
                if (item is { })
                {
                    rows.AddRange(item);
                }
            }

            return rows;
        }
        
        public async Task<IImmutableList<Exception>> AsyncWriteMessages(
            IEnumerable<AtomicWrite> messages,
            long timeStamp = 0)
        {
            var serializedTries = Serializer.Serialize(messages, timeStamp);
            
            // Fold our List of Lists into a single sequence
            var rows = Seq(FlattenListOfListsToList(serializedTries));
            
            // Wait for the write to go through. If Task fails, write will be captured as WriteMessagesFailure.
            await QueueWriteJournalRows(rows);
            
            // If we get here, we build an ImmutableList containing our rejections.
            // These will be captured as WriteMessagesRejected
            return BuildWriteRejections(serializedTries);
        }

        protected static ImmutableList<Exception> BuildWriteRejections(List<Util.Try<JournalRow[]>> serializedTries)
        {
            var builderEx = new Exception[serializedTries.Count];
            for (var i = 0; i < serializedTries.Count; i++)
            {
                builderEx[i] = (serializedTries[i].Failure.Value);
            }
            return ImmutableList.CreateRange(builderEx);
        }
        
        protected static ImmutableList<Exception> FailWriteThrowHelper(Exception e)
        {
            throw TryUnwrapException(e);
        }
        
        protected static Exception TryUnwrapException(Exception e)
        {
            if (e is AggregateException aggregateException)
            {
                aggregateException = aggregateException.Flatten();
                if (aggregateException.InnerExceptions.Count == 1)
                    return aggregateException.InnerExceptions[0];
            }

            return e;
        }

        public async Task Delete(string persistenceId, long maxSequenceNr)
        {
            if (LogicalDelete)
            {
                var _ = _logWarnAboutLogicalDeletionDeprecation.Value;
            }

            await using var db = ConnectionFactory.GetConnection();
            
            var transaction = await db.BeginTransactionAsync();
            try
            {
                await db.GetTable<JournalRow>()
                    .Where(r => r.PersistenceId == persistenceId && r.SequenceNumber <= maxSequenceNr)
                    .Set(r => r.Deleted, true)
                    .UpdateAsync();
                
                var maxMarkedDeletion =
                    await MaxMarkedForDeletionMaxPersistenceIdQuery(db, persistenceId).FirstOrDefaultAsync();
                
                if (JournalConfig.DaoConfig.SqlCommonCompatibilityMode)
                {
                    await db.GetTable<JournalMetaData>()
                        .InsertOrUpdateAsync(
                            insertSetter: () => new JournalMetaData
                            {
                                PersistenceId = persistenceId,
                                SequenceNumber = maxMarkedDeletion
                            },
                            onDuplicateKeyUpdateSetter: jmd => new JournalMetaData(),
                            keySelector: () => new JournalMetaData
                            {
                                PersistenceId = persistenceId,
                                SequenceNumber = maxMarkedDeletion
                            });
                }

                if (LogicalDelete == false)
                {
                    await db.GetTable<JournalRow>()
                        .Where(r =>
                            r.PersistenceId == persistenceId &&
                            r.SequenceNumber <= maxSequenceNr &&
                            r.SequenceNumber < maxMarkedDeletion)
                        .DeleteAsync();
                }

                if (JournalConfig.DaoConfig.SqlCommonCompatibilityMode)
                {
                    await db.GetTable<JournalMetaData>()
                        .Where(r =>
                            r.PersistenceId == persistenceId &&
                            r.SequenceNumber < maxMarkedDeletion)
                        .DeleteAsync();
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                Logger.Error(ex,"Error on delete!");
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception exception)
                {
                    throw new AggregateException(exception, ex);
                }
                    
                throw;
            }
        }
        
        protected IQueryable<long> MaxMarkedForDeletionMaxPersistenceIdQuery(
            DataConnection connection,
            string persistenceId)
        {
            return connection.GetTable<JournalRow>()
                    .Where(r => r.PersistenceId == persistenceId && r.Deleted)
                    .OrderByDescending(r => r.SequenceNumber)
                    .Select(r => r.SequenceNumber).Take(1);
        }

        protected static readonly Expression<Func<JournalMetaData, PersistenceIdAndSequenceNumber>> MetaDataSelector = md =>
            new PersistenceIdAndSequenceNumber(md.SequenceNumber, md.PersistenceId);

        protected static readonly Expression<Func<JournalRow, PersistenceIdAndSequenceNumber>> RowDataSelector = md =>
            new PersistenceIdAndSequenceNumber(md.SequenceNumber, md.PersistenceId);
        
        private IQueryable<long?> MaxSeqNumberForPersistenceIdQuery(
            DataConnection db, 
            string persistenceId,
            long minSequenceNumber = 0)
        {
            if (minSequenceNumber != 0)
            {
                return JournalConfig.DaoConfig.SqlCommonCompatibilityMode 
                    ? MaxSeqForPersistenceIdQueryableCompatibilityModeWithMinId(db, persistenceId, minSequenceNumber) 
                    : MaxSeqForPersistenceIdQueryableNativeModeMinId(db, persistenceId, minSequenceNumber);
            }
            
            return JournalConfig.DaoConfig.SqlCommonCompatibilityMode 
                ? MaxSeqForPersistenceIdQueryableCompatibilityMode(db, persistenceId) 
                : MaxSeqForPersistenceIdQueryableNativeMode(db, persistenceId);
        }

        private static IQueryable<long?> MaxSeqForPersistenceIdQueryableNativeMode(
            DataConnection db,
            string persistenceId)
        {
            return db.GetTable<JournalRow>()
                .Where(r => r.PersistenceId == persistenceId)
                .Select(r => (long?)r.SequenceNumber);
        }

        private static IQueryable<long?> MaxSeqForPersistenceIdQueryableNativeModeMinId(
            DataConnection db, 
            string persistenceId, 
            long minSequenceNumber)
        {
            return db.GetTable<JournalRow>()
                .Where(r =>
                    r.PersistenceId == persistenceId &&
                    r.SequenceNumber > minSequenceNumber)
                .Select(r => (long?)r.SequenceNumber);
        }

        private static IQueryable<long?> MaxSeqForPersistenceIdQueryableCompatibilityModeWithMinId(
            DataConnection db, 
            string persistenceId,
            long minSequenceNumber)
        {
            return db.GetTable<JournalRow>()
                .Where(r =>
                    r.PersistenceId == persistenceId &&
                    r.SequenceNumber > minSequenceNumber)
                .Select(r => LinqToDB.Sql.Ext.Max<long?>(r.SequenceNumber).ToValue())
                .Union(db
                    .GetTable<JournalMetaData>()
                    .Where(r =>
                        r.SequenceNumber > minSequenceNumber &&
                        r.PersistenceId == persistenceId)
                    .Select(r => LinqToDB.Sql.Ext.Max<long?>(r.SequenceNumber).ToValue()));
        }

        private static IQueryable<long?> MaxSeqForPersistenceIdQueryableCompatibilityMode(
            DataConnection db, 
            string persistenceId)
        {
            return db.GetTable<JournalRow>()
                .Where(r => r.PersistenceId == persistenceId)
                .Select(r => LinqToDB.Sql.Ext.Max<long?>(r.SequenceNumber).ToValue())
                .Union(db
                    .GetTable<JournalMetaData>()
                    .Where(r => r.PersistenceId == persistenceId)
                    .Select(r => LinqToDB.Sql.Ext.Max<long?>(r.SequenceNumber).ToValue()));
        }

        private static readonly Expression<Func<PersistenceIdAndSequenceNumber, long>> SequenceNumberSelector = r => 
            r.SequenceNumber;

        public async Task<Done> Update(string persistenceId, long sequenceNr, object payload)
        {
            var write = new Persistent(payload, sequenceNr, persistenceId);
            var serialize = Serializer.Serialize(write);
            if (serialize.IsSuccess)
            {
                throw new ArgumentException(
                    $"Failed to serialize {write.GetType()} for update of {persistenceId}] @ {sequenceNr}",
                    serialize.Failure.Value);
            }

            await using var db = ConnectionFactory.GetConnection();
            
            await db.GetTable<JournalRow>()
                .Where(r =>
                    r.PersistenceId == persistenceId &&
                    r.SequenceNumber == write.SequenceNr)
                .Set(r => r.Message, serialize.Get().Message)
                .UpdateAsync();
            
            return Done.Instance;
        }

        public async Task<long> HighestSequenceNr(string persistenceId, long fromSequenceNr)
        {
            await using var db = ConnectionFactory.GetConnection();
            
            return (await MaxSeqNumberForPersistenceIdQuery(db, persistenceId, fromSequenceNr).MaxAsync())
                .GetValueOrDefault(0);
        }

        

        /// <summary>
        /// This override is greedy since it is always called
        /// from within <see cref="BaseJournalDaoWithReadMessages.MessagesWithBatch"/>
        /// </summary>
        /// <param name="db"></param>
        /// <param name="persistenceId"></param>
        /// <param name="fromSequenceNr"></param>
        /// <param name="toSequenceNr"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public override Source<Util.Try<ReplayCompletion>, NotUsed> Messages(
            DataConnection db, 
            string persistenceId,
            long fromSequenceNr, 
            long toSequenceNr,
            long max)
        {
            IQueryable<JournalRow> query = db.GetTable<JournalRow>()
                .Where(r =>
                    r.PersistenceId == persistenceId &&
                    r.SequenceNumber >= fromSequenceNr &&
                    r.SequenceNumber <= toSequenceNr &&
                    r.Deleted == false)
                .OrderBy(r => r.SequenceNumber);
            
            if (max <= int.MaxValue)
            {
                query = query.Take((int) max);
            }
                
            return Source.FromTask(query.ToListAsync())
                .SelectMany(r => r)
                .Via(_deserializeFlowMapped);
            //return AsyncSource<JournalRow>.FromEnumerable(query,async q=>await q.ToListAsync())
            //    .Via(
            //        deserializeFlow).Select(MessageWithBatchMapper());
        }

        private static Func<Util.Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, Util.Try<ReplayCompletion>> MessageWithBatchMapper() =>
            sertry => sertry.IsSuccess 
                ? new Util.Try<ReplayCompletion>(new ReplayCompletion( sertry.Success.Value)) 
                : new Util.Try<ReplayCompletion>(sertry.Failure.Value);
    }

    public sealed class PersistenceIdAndSequenceNumber
    {
        public PersistenceIdAndSequenceNumber(long sequenceNumber, string persistenceId)
        {
            SequenceNumber = sequenceNumber;
            PersistenceId = persistenceId;
        }
        
        public long SequenceNumber { get; }
        public string PersistenceId { get; }
    }
}