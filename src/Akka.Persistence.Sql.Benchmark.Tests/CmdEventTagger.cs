// -----------------------------------------------------------------------
//  <copyright file="CmdEventTagger.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using Akka.Persistence.Journal;

namespace Akka.Persistence.Sql.Benchmark.Tests
{
    /// <summary>
    ///     Write event adapter that tags every <see cref="Cmd"/> with two consistent tags.
    ///     Used by tagged perf spec variants to exercise the tag insert pipeline.
    /// </summary>
    public sealed class CmdEventTagger : IWriteEventAdapter
    {
        public const string Tag1 = "perf-tag-1";
        public const string Tag2 = "perf-tag-2";

        private static readonly string[] Tags = { Tag1, Tag2 };
        private static readonly ImmutableHashSet<string> TagsSet = ImmutableHashSet.Create(Tag1, Tag2);

        public string Manifest(object evt) => string.Empty;

        public object ToJournal(object evt)
            => evt is Cmd ? new Tagged(evt, TagsSet) : evt;
    }
}

