namespace Akka.Persistence.Sql.Linq2Db.Query.InternalProtocol
{
    public sealed class QueryOrderingIdsTimerKey
    {
        public static readonly QueryOrderingIdsTimerKey Instance = new ();

        private QueryOrderingIdsTimerKey()
        {
        }
    }
}