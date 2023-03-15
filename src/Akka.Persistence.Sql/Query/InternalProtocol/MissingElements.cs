// -----------------------------------------------------------------------
//  <copyright file="MissingElements.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using LanguageExt;

namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public sealed class MissingElements
    {
        public static readonly MissingElements Empty = new(Seq<NumericRangeEntry>.Empty);

        public MissingElements(Seq<NumericRangeEntry> elements)
            => Elements = elements;

        public bool Isempty
            => Elements.IsEmpty;

        public Seq<NumericRangeEntry> Elements { get; }

        public MissingElements AddRange(long from, long until)
            => new(Elements.Add(new NumericRangeEntry(from, until)));

        public bool Contains(long id)
            => Elements.Any(r => r.InRange(id));
    }
}
