// -----------------------------------------------------------------------
//  <copyright file="EventsByTagResponseItem.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public sealed class EventsByTagResponseItem
    {
        public EventsByTagResponseItem(
            IPersistentRepresentation representation,
            ImmutableHashSet<string> tags,
            long sequenceNr)
        {
            Representation = representation;
            Tags = tags;
            SequenceNr = sequenceNr;
        }

        public IPersistentRepresentation Representation { get; }

        public ImmutableHashSet<string> Tags { get; }

        public long SequenceNr { get; }
    }
}
