﻿using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Streams.Dsl;
using Akka.Util;
using LinqToDB.Data;

namespace Akka.Persistence.Sql.Journal.Dao
{
    public interface IJournalDaoWithReadMessages
    {
        Source<Try<ReplayCompletion>, NotUsed> Messages(
            DataConnection dc,
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            long max);

        Source<Try<ReplayCompletion>,NotUsed> MessagesWithBatch(
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            int batchSize,
            Option<(TimeSpan,IScheduler)> refreshInterval);
    }
}
