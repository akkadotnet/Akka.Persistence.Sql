// -----------------------------------------------------------------------
//  <copyright file="IJournalDaoWithReadMessages.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Streams.Dsl;
using Akka.Util;

namespace Akka.Persistence.Sql.Journal.Dao
{
    public interface IJournalDaoWithReadMessages
    {
        Source<Try<ReplayCompletion>, NotUsed> Messages(
            AkkaDataConnection connection,
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            long max);

        Source<Try<ReplayCompletion>, NotUsed> MessagesWithBatch(
            string persistenceId,
            long fromSequenceNr,
            long toSequenceNr,
            int batchSize,
            Option<(TimeSpan, IScheduler)> refreshInterval);
    }
}
