﻿// -----------------------------------------------------------------------
//  <copyright file="BaseJournalDaoWithReadMessages.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Pattern;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Streams;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;
using LinqToDB.Data;

namespace Akka.Persistence.Sql.Journal.Dao
{
    public abstract class BaseJournalDaoWithReadMessages : IJournalDaoWithReadMessages
    {
        protected readonly AkkaPersistenceDataConnectionFactory ConnectionFactory;

        protected readonly IMaterializer Materializer;

        protected readonly IAdvancedScheduler Scheduler;

        protected BaseJournalDaoWithReadMessages(
            IAdvancedScheduler scheduler,
            IMaterializer materializer,
            AkkaPersistenceDataConnectionFactory connectionFactory)
        {
            Scheduler = scheduler;
            Materializer = materializer;
            ConnectionFactory = connectionFactory;
        }

        public abstract Source<Try<ReplayCompletion>, NotUsed> Messages(
            DataConnection connection,
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            long max);

        public Source<Try<ReplayCompletion>, NotUsed> MessagesWithBatch(
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            int batchSize,
            Option<(TimeSpan, IScheduler)> refreshInterval)
            => Source
                .UnfoldAsync<(long seqNr, FlowControlEnum flowControl), LanguageExt.Seq<Try<ReplayCompletion>>>(
                    (Math.Max(1, fromSequenceNr), FlowControlEnum.Continue),
                    async opt =>
                    {
                        async Task<LanguageExt.Seq<Try<ReplayCompletion>>> BatchFromDb(
                            string s,
                            long l,
                            int i,
                            long fromSeqNo)
                        {
                            await using var connection = ConnectionFactory.GetConnection();

                            return await Messages(connection, s, fromSeqNo, l, i)
                                .RunWith(ExtSeq.Seq<Try<ReplayCompletion>>(), Materializer);
                        }

                        async Task<Option<((long, FlowControlEnum), LanguageExt.Seq<Try<ReplayCompletion>>)>>
                            RetrieveNextBatch(long fromSeq)
                        {
                            var msg = await BatchFromDb(persistenceId, toSequenceNr, batchSize, fromSeq);

                            var hasMoreEvents = msg.Count == batchSize;

                            var lastSeq = Option<long>.None;
                            if (msg.IsEmpty == false)
                                lastSeq = msg.Last.Get().Repr.SequenceNr;

                            FlowControlEnum nextControl;
                            if ((lastSeq.HasValue && lastSeq.Value >= toSequenceNr) || opt.seqNr > toSequenceNr)
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
                                nextFrom = lastSeq.Value + 1;

                            return new Option<((long, FlowControlEnum), LanguageExt.Seq<Try<ReplayCompletion>>)>(
                                ((nextFrom, nextControl), msg));
                        }

                        return opt.flowControl switch
                        {
                            FlowControlEnum.Stop =>
                                Option<((long, FlowControlEnum), LanguageExt.Seq<Try<ReplayCompletion>>)>.None,

                            FlowControlEnum.Continue =>
                                await RetrieveNextBatch(opt.seqNr),

                            FlowControlEnum.ContinueDelayed when refreshInterval.HasValue =>
                                await FutureTimeoutSupport.After(
                                    refreshInterval.Value.Item1,
                                    refreshInterval.Value.Item2,
                                    () => RetrieveNextBatch(opt.seqNr)),

                            _ => InvalidFlowThrowHelper(opt)
                        };
                    })
                .SelectMany(r => r);

        private static Option<long> MessagesWithBatchThrowHelper(Try<ReplayCompletion> lastMsg)
            => throw lastMsg.Failure.Value;

        private static Option<((long, FlowControlEnum), LanguageExt.Seq<Try<ReplayCompletion>>)> InvalidFlowThrowHelper(
            (long, FlowControlEnum) opt)
            => throw new Exception($"Got invalid FlowControl from Queue! Type : {opt.Item2}");
    }
}
