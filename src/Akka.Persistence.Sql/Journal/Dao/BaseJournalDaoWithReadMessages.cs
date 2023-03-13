using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Pattern;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Streams;
using Akka.Streams;
using Akka.Streams.Dsl;
using LanguageExt;
using LinqToDB.Data;

namespace Akka.Persistence.Sql.Journal.Dao
{
    public abstract class BaseJournalDaoWithReadMessages : IJournalDaoWithReadMessages
    {
        protected readonly AkkaPersistenceDataConnectionFactory ConnectionFactory;
        protected BaseJournalDaoWithReadMessages(
            IAdvancedScheduler scheduler,
            IMaterializer materializer,
            AkkaPersistenceDataConnectionFactory connectionFactory)
        {
            Scheduler = scheduler;
            Materializer = materializer;
            ConnectionFactory = connectionFactory;
        }

        protected readonly IAdvancedScheduler Scheduler;
        protected readonly IMaterializer Materializer;

        public abstract Source<Util.Try<ReplayCompletion>, NotUsed> Messages(
            DataConnection db,
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            long max);

        public Source<Util.Try<ReplayCompletion>, NotUsed> MessagesWithBatch(
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            int batchSize,
            Util.Option<(TimeSpan,IScheduler)> refreshInterval)
        {
            return Source
                .UnfoldAsync<(long seqNr, FlowControlEnum flowControl), Seq<Util.Try<ReplayCompletion>>>(
                    (Math.Max(1, fromSequenceNr), FlowControlEnum.Continue),
                    async opt =>
                    {
                        async Task<Seq<Util.Try<ReplayCompletion>>> BatchFromDb(string s, long l, int i, long fromSeqNo)
                        {
                            Seq<Util.Try<ReplayCompletion>> msg;
                            await using var conn = ConnectionFactory.GetConnection();
                            msg = await Messages(conn, s, fromSeqNo, l, i)
                                .RunWith(ExtSeq.Seq<Util.Try<ReplayCompletion>>(), Materializer);

                            return msg;
                        }

                        async Task<Util.Option<((long, FlowControlEnum), Seq<Util.Try<ReplayCompletion>>)>> RetrieveNextBatch(long fromSeq)
                        {
                            Seq<Util.Try<ReplayCompletion>> msg;
                            msg = await BatchFromDb(persistenceId, toSequenceNr, batchSize, fromSeq);

                            var hasMoreEvents = msg.Count == batchSize;
                            //var lastMsg = msg.IsEmpty.LastOrDefault();
                            var lastSeq = Util.Option<long>.None;
                            if (msg.IsEmpty == false)
                            {
                                lastSeq = msg.Last.Get().Repr.SequenceNr;
                            }

                            FlowControlEnum nextControl;
                            if ((lastSeq.HasValue && lastSeq.Value >= toSequenceNr) || opt.Item1 > toSequenceNr)
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

                            var nextFrom = opt.seqNr;
                            if (lastSeq.HasValue)
                            {
                                nextFrom = lastSeq.Value + 1;
                            }

                            return new Util.Option<((long, FlowControlEnum), Seq<Util.Try<ReplayCompletion>>)>(((nextFrom, nextControl), msg));
                        }

                        return opt.flowControl switch
                        {
                            FlowControlEnum.Stop =>
                                Util.Option<((long, FlowControlEnum), Seq<Util.Try<ReplayCompletion>>)>.None,

                            FlowControlEnum.Continue => await RetrieveNextBatch(opt.seqNr),

                            FlowControlEnum.ContinueDelayed when refreshInterval.HasValue =>
                                await FutureTimeoutSupport.After(refreshInterval.Value.Item1, refreshInterval.Value.Item2, () => RetrieveNextBatch(opt.seqNr)),

                            _ => InvalidFlowThrowHelper(opt)
                        };
                    })
                .SelectMany(r => r);
        }

        private static Util.Option<long> MessagesWithBatchThrowHelper(Util.Try<ReplayCompletion> lastMsg)
        {
            throw lastMsg.Failure.Value;
        }

        private static Util.Option<((long, FlowControlEnum), Seq<Util.Try<ReplayCompletion>>)> InvalidFlowThrowHelper((long, FlowControlEnum) opt)
        {
            throw new Exception($"Got invalid FlowControl from Queue! Type : {opt.Item2.ToString()}");
        }
    }
}
