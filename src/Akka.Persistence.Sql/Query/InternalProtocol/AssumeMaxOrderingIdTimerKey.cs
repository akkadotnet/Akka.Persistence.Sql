namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public sealed class AssumeMaxOrderingIdTimerKey
    {
        public static AssumeMaxOrderingIdTimerKey Instance => new ();
        private AssumeMaxOrderingIdTimerKey()
        { }
    }
}
