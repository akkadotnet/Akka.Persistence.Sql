using System.Collections.Immutable;

namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public sealed class NewOrderingIds
    {
        public long MaxOrdering { get; }
        public IImmutableList<long> Elements { get; }

        public NewOrderingIds(long currentMaxOrdering, IImmutableList<long> res)
        {
            MaxOrdering = currentMaxOrdering;
            Elements = res;
        }
    }
}
