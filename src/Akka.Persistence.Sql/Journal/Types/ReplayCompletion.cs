// -----------------------------------------------------------------------
//  <copyright file="ReplayCompletion.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using Akka.Annotations;

namespace Akka.Persistence.Sql.Journal.Types
{
    [InternalApi]
    public sealed class ReplayCompletion
    {
        public readonly long Ordering;

        public readonly IPersistentRepresentation Representation;

        public readonly string[] Tags;

        public ReplayCompletion(
            IPersistentRepresentation representation,
            string[] tags,
            long ordering)
        {
            Representation = representation;
            Ordering = ordering;
            Tags = tags;
        }

        public ReplayCompletion((IPersistentRepresentation, string[], long) success)
        {
            (Representation, Tags, Ordering) = success;
        }
    }
}
