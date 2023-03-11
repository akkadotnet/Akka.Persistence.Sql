namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public sealed class QueryOrderingIdsTimerKey
    {
        public static readonly QueryOrderingIdsTimerKey Instance = new ();

        private QueryOrderingIdsTimerKey()
        {
        }
    }
}
