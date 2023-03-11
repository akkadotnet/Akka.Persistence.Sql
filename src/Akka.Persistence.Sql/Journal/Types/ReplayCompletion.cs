using System.Collections.Immutable;

namespace Akka.Persistence.Sql.Journal.Types
{
    public sealed class ReplayCompletion
    {
        public ReplayCompletion(IPersistentRepresentation repr, long ordering)
        {
            Repr = repr;
            Ordering = ordering;
        }

        public readonly IPersistentRepresentation Repr;
        public readonly long Ordering;

        public ReplayCompletion((IPersistentRepresentation, IImmutableSet<string>, long) success)
        {
            (Repr, _, Ordering) = success;
        }
    }
}
