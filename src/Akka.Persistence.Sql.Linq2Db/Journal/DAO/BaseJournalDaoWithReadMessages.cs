using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Pattern;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Streams;
using Akka.Streams;
using Akka.Streams.Dsl;
using LanguageExt;
using LinqToDB.Data;

namespace Akka.Persistence.Sql.Linq2Db.Journal.DAO
{
    public abstract class BaseJournalDaoWithReadMessages : IJournalDaoWithReadMessages
    {
        protected readonly AkkaPersistenceDataConnectionFactory _connectionFactory;
        protected BaseJournalDaoWithReadMessages(IAdvancedScheduler ec,
            IMaterializer mat, AkkaPersistenceDataConnectionFactory connectionFactory)
        {
            this.ec = ec;
            this.mat = mat;
            _connectionFactory = connectionFactory;
        }
        protected IAdvancedScheduler ec;
        protected IMaterializer mat;

        public abstract Source<Util.Try<ReplayCompletion>, NotUsed> Messages(DataConnection db, string persistenceId, long fromSequenceNr, long toSequenceNr,
            long max);
        

        
        public Source<Util.Try<ReplayCompletion>, NotUsed> MessagesWithBatch(string persistenceId, long fromSequenceNr,
            long toSequenceNr, int batchSize, Util.Option<(TimeSpan,IScheduler)> refreshInterval)
        {
            return Source
                .UnfoldAsync<(long, FlowControlEnum),
                    Seq<Util.Try<ReplayCompletion>>>(
                    (Math.Max(1, fromSequenceNr),
                        FlowControlEnum.Continue),
                    async opt =>
                    {
                        async Task<Util.Option<((long, FlowControlEnum), Seq<Util.Try<ReplayCompletion>>)>>
                            RetrieveNextBatch()
                        {
                            Seq<
                                Util.Try<ReplayCompletion>> msg;
                            using (var conn =
                                _connectionFactory.GetConnection())
                            {
                                msg = await Messages(conn, persistenceId,
                                        opt.Item1,
                                        toSequenceNr, batchSize)
                                    .RunWith(
                                            ExtSeq.Seq<Util.Try<ReplayCompletion>>(), mat);
                            }

                            var hasMoreEvents = msg.Count == batchSize;
                            //var lastMsg = msg.IsEmpty.LastOrDefault();
                            Util.Option<long> lastSeq = Util.Option<long>.None;
                            if (msg.IsEmpty == false)
                            {
                                
                                lastSeq = msg.Last.Get().Repr.SequenceNr;
                            }

                            
                            FlowControlEnum nextControl = FlowControlEnum.Unknown;
                            if ((lastSeq.HasValue &&
                                lastSeq.Value >= toSequenceNr) || opt.Item1 > toSequenceNr)
                            {
                                nextControl = FlowControlEnum.Stop;
                            }
                            else if (hasMoreEvents)
                            {
                                nextControl = FlowControlEnum.Continue;
                            }
                            else if (refreshInterval.HasValue == false)
                            {
                                nextControl = FlowControlEnum.Stop;
                            }
                            else
                            {
                                nextControl = FlowControlEnum.ContinueDelayed;
                            }

                            long nextFrom = opt.Item1;
                            if (lastSeq.HasValue)
                            {
                                nextFrom = lastSeq.Value + 1;
                            }

                            return new Util.Option<((long, FlowControlEnum), Seq<Util.Try<ReplayCompletion>>)>((
                                    (nextFrom, nextControl), msg));
                        }

                        switch (opt.Item2)
                        {
                            case FlowControlEnum.Stop:
                                return Util.Option<((long, FlowControlEnum), Seq<Util.Try<ReplayCompletion>>)>.None;
                            case FlowControlEnum.Continue:
                                return await RetrieveNextBatch();
                            case FlowControlEnum.ContinueDelayed when refreshInterval.HasValue:
                                return await FutureTimeoutSupport.After(refreshInterval.Value.Item1,refreshInterval.Value.Item2, RetrieveNextBatch);
                            default:
                                return InvalidFlowThrowHelper(opt);
                        }
                    }).SelectMany(r => r);;
        }

        private static Util.Option<long> MessagesWithBatchThrowHelper(Util.Try<ReplayCompletion> lastMsg)
        {
            throw lastMsg.Failure.Value;
        }

        private static Util.Option<((long, FlowControlEnum), Seq<Util.Try<ReplayCompletion>>)> InvalidFlowThrowHelper((long, FlowControlEnum) opt)
        {
            throw new Exception(
                $"Got invalid FlowControl from Queue! Type : {opt.Item2.ToString()}");
        }
    }
}