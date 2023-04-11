// -----------------------------------------------------------------------
//  <copyright file="DebuggingHelpers.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Diagnostics;
using Akka.Event;
using LinqToDB.Data;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmarks
{
    public static class DebuggingHelpers
    {
        public static void TraceDumpOn(ILoggingAdapter log)
        {
            DataConnection.TurnTraceSwitchOn(TraceLevel.Verbose);

            DataConnection.WriteTraceLine = (message, category, level) =>
                log.Info($"[{level}] {message} {category}");
        }

        public static void TraceDumpOff()
        {
            DataConnection.TurnTraceSwitchOn(TraceLevel.Off);
        }
    }
}
