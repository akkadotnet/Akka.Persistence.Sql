// -----------------------------------------------------------------------
//  <copyright file="TimeSpanExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Utility
{
    /// <summary>
    ///     TimeSpanExtensions
    /// </summary>
    internal static class TimeSpanExtensions
    {
        /// <summary>
        ///     Multiplies a timespan by an integer value
        /// </summary>
        internal static TimeSpan Multiply(this TimeSpan multiplicand, int multiplier)
            => TimeSpan.FromTicks(multiplicand.Ticks * multiplier);

        /// <summary>
        ///     Multiplies a timespan by a double value
        /// </summary>
        internal static TimeSpan Multiply(this TimeSpan multiplicand, double multiplier)
            => TimeSpan.FromTicks((long)(multiplicand.Ticks * multiplier));
    }
}
