// -----------------------------------------------------------------------
//  <copyright file="ReplayCompletion.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

namespace Akka.Persistence.Sql.Journal.Types
{
    public sealed class ReplayCompletion
    {
        public readonly long Ordering;

        public readonly IPersistentRepresentation Representation;

        public ReplayCompletion(
            IPersistentRepresentation representation,
            long ordering)
        {
            Representation = representation;
            Ordering = ordering;
        }

        public ReplayCompletion((IPersistentRepresentation, IImmutableSet<string>, long) success)
            => (Representation, _, Ordering) = success;
    }
}
