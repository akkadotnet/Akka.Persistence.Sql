namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public sealed class MaxOrderingId
    {
        public MaxOrderingId(long max)
        {
            Max = max;
        }

        public long Max { get; }
    }
}
