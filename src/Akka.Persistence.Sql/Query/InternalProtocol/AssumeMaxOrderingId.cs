namespace Akka.Persistence.Sql.Linq2Db.Query.InternalProtocol
{
    public sealed class AssumeMaxOrderingId
    {
        public AssumeMaxOrderingId(long max)
        {
            Max = max;
        }

        public long Max { get; }
    }
}