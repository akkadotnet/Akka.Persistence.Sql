// -----------------------------------------------------------------------
//  <copyright file="ColorFruitTagger.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Linq;
using Akka.Persistence.Journal;

namespace Akka.Persistence.Sql.Tests
{
    public class ColorFruitTagger : IEventAdapter
    {
        public static IImmutableSet<string> Colors { get; } = ImmutableHashSet.Create("green", "black", "blue");
        public static IImmutableSet<string> Fruits { get; } = ImmutableHashSet.Create("apple", "banana");

        public string Manifest(object evt) => string.Empty;

        public object ToJournal(object evt)
        {
            if (evt is not string s)
                return evt;

            var colorTags = Colors.Aggregate(
                ImmutableHashSet<string>.Empty,
                (acc, color) => s.Contains(color)
                    ? acc.Add(color)
                    : acc);
            var fruitTags = Fruits.Aggregate(
                ImmutableHashSet<string>.Empty,
                (acc, color) => s.Contains(color)
                    ? acc.Add(color)
                    : acc);
            var tags = colorTags.Union(fruitTags);
            return tags.IsEmpty
                ? evt
                : new Tagged(evt, tags);
        }

        public IEventSequence FromJournal(object evt, string manifest)
        {
            if (evt is not string s)
                return EventSequence.Single(evt);

            if (s.Contains("invalid"))
                return EventSequence.Empty;

            if (s.Contains("duplicated"))
                return EventSequence.Create(evt + "-1", evt + "-2");

            return EventSequence.Single(evt);
        }
    }
}

