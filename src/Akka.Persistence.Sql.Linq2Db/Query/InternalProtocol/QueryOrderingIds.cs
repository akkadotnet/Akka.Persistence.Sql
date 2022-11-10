namespace Akka.Persistence.Sql.Linq2Db.Query.InternalProtocol
{
    public sealed class QueryOrderingIds
    {
        public static readonly QueryOrderingIds Instance = new ();

        private QueryOrderingIds()
        {
        }
    }
}