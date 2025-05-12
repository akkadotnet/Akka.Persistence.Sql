// -----------------------------------------------------------------------
//  <copyright file="IJournalDao.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

namespace Akka.Persistence.Sql.Journal.Dao
{
    public interface IJournalDao : IJournalDaoWithReadMessages
    {
        Task Delete(
            string persistenceId,
            long toSequenceNr, 
            CancellationToken cancellationToken);

        Task<long> HighestSequenceNr(
            string persistenceId,
            long fromSequenceNr, 
            CancellationToken cancellationToken);

        Task<IImmutableList<Exception>> AsyncWriteMessages(
            IEnumerable<AtomicWrite> messages,
            CancellationToken cancellationToken,
            long timeStamp = 0);
    }
}
