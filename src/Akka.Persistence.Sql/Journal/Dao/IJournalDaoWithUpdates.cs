// -----------------------------------------------------------------------
//  <copyright file="IJournalDaoWithUpdates.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Akka.Persistence.Sql.Journal.Dao
{
    public interface IJournalDaoWithUpdates : IJournalDao
    {
        Task<Done> Update(
            string persistenceId,
            long sequenceNr,
            object payload);
    }
}
