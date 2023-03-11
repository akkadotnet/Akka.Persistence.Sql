using System.Collections.Immutable;

namespace Akka.Persistence.Sql.Linq2Db.Query.InternalProtocol
{
    public sealed class EventsByTagResponseItem
    {
        public EventsByTagResponseItem(IPersistentRepresentation repr, ImmutableHashSet<string> tags, long sequenceNr)
        {
            Repr = repr;
            Tags = tags;
            SequenceNr = sequenceNr;
        }

        public IPersistentRepresentation Repr { get; }
        public ImmutableHashSet<string> Tags { get; }
        public long SequenceNr { get; }
    }
}