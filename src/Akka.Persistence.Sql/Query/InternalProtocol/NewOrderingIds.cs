// -----------------------------------------------------------------------
//  <copyright file="NewOrderingIds.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public sealed class NewOrderingIds
    {
        public NewOrderingIds(long currentMaxOrdering, IImmutableList<long> res)
        {
            MaxOrdering = currentMaxOrdering;
            Elements = res;
        }

        public long MaxOrdering { get; }

        public IImmutableList<long> Elements { get; }
    }
}
