namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public class GetMaxOrderingId
    {
        public static readonly GetMaxOrderingId Instance = new ();

        private GetMaxOrderingId()
        {
        }
    }
}
