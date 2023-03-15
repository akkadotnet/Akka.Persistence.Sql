// -----------------------------------------------------------------------
//  <copyright file="WriteFinished.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;

namespace Akka.Persistence.Sql.Journal.Types
{
    public sealed class WriteFinished
    {
        public WriteFinished(string persistenceId, Task future)
        {
            PersistenceId = persistenceId;
            Future = future;
        }

        public string PersistenceId { get; }

        public Task Future { get; }
    }
}
