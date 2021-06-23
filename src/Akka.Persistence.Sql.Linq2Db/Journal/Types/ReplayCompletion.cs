using System.Collections.Immutable;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Types
{
    public class ReplayCompletion
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
            //(Repr, _, Ordering) = success;
            Repr = success.Item1;
            Ordering = success.Item3;
        }
    }
}