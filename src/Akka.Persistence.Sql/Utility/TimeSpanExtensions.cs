// -----------------------------------------------------------------------
//  <copyright file="TimeSpanExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Utility
{
    public static class TimeSpanExtensions
    {
        public static TimeSpan Multiply(this TimeSpan timespan, double multiplier)
            => new ((long)(timespan.Ticks * multiplier));
    }
}
