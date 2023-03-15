﻿// -----------------------------------------------------------------------
//  <copyright file="QueryOrderingIdsTimerKey.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public sealed class QueryOrderingIdsTimerKey
    {
        public static readonly QueryOrderingIdsTimerKey Instance = new();

        private QueryOrderingIdsTimerKey() { }
    }
}
