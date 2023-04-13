// -----------------------------------------------------------------------
//  <copyright file="Const.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Benchmarks
{
    internal static class Const
    {
        public const int TotalMessages = 3000000;
        public const int TagLowerBound = 2 * (TotalMessages / 3);
        public const int TagUpperBound = TagLowerBound + 10;
    }
}
