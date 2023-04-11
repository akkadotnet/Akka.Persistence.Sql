// -----------------------------------------------------------------------
//  <copyright file="EventTagger.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Journal;

namespace Akka.Persistence.Sql.Benchmark.Common
{
    public sealed class EventTagger : IWriteEventAdapter
    {
        public string Manifest(object evt) => string.Empty;

        public object ToJournal(object evt)
        {
            if (evt is not int i || i is <= Const.TagLowerBound or > Const.TagUpperBound)
                return evt;

            return new Tagged(i, new[] { "TAG" });
        }
    }
}
