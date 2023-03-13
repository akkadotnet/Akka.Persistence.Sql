using LanguageExt;

namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public sealed class MissingElements
    {
        public MissingElements(Seq<NumericRangeEntry> elements)
        {
            Elements = elements;
        }

        public MissingElements AddRange(long from, long until)
        {
            return new MissingElements(Elements.Add(new NumericRangeEntry(from, until)));
        }

        public bool Contains(long id)
        {
            return Elements.Any(r => r.InRange(id));
        }

        public bool Isempty => Elements.IsEmpty;
        public Seq<NumericRangeEntry> Elements { get; }

        public static readonly MissingElements Empty = new( Seq<NumericRangeEntry>.Empty );
    }
}
