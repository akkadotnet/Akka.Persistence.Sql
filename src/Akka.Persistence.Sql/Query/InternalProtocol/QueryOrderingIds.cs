namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public sealed class QueryOrderingIds
    {
        public static readonly QueryOrderingIds Instance = new ();

        private QueryOrderingIds()
        {
        }
    }
}
