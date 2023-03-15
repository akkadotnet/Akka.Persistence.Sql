// -----------------------------------------------------------------------
//  <copyright file="ScheduleAssumeMaxOrderingId.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public sealed class ScheduleAssumeMaxOrderingId
    {
        public ScheduleAssumeMaxOrderingId(long maxInDatabase)
            => MaxInDatabase = maxInDatabase;

        public long MaxInDatabase { get; }
    }
}
