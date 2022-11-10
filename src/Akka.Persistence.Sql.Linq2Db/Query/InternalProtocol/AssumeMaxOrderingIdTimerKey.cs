namespace Akka.Persistence.Sql.Linq2Db.Query.InternalProtocol
{
    public sealed class AssumeMaxOrderingIdTimerKey
    {
        public static AssumeMaxOrderingIdTimerKey Instance => new ();
        private AssumeMaxOrderingIdTimerKey()
        { }
    }
}