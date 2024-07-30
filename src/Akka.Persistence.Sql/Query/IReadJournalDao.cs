// -----------------------------------------------------------------------
//  <copyright file="IReadJournalDao.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Journal.Dao;
using Akka.Streams.Dsl;
using Akka.Util;

namespace Akka.Persistence.Sql.Query
{
    public interface IReadJournalDao : IJournalDaoWithReadMessages
    {
        Source<string, NotUsed> AllPersistenceIdsSource(
            long max);

        Source<Try<(IPersistentRepresentation, string[], long)>, NotUsed> EventsByTag(
            string tag,
            long offset,
            long maxOffset,
            long max);

        Source<long, NotUsed> JournalSequence(
            long offset,
            long limit);

        Task<long> MaxJournalSequenceAsync();
    }
}
