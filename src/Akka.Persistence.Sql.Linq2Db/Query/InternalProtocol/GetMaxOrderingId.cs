namespace Akka.Persistence.Sql.Linq2Db.Query.InternalProtocol
{
    public class GetMaxOrderingId
    {
        public static readonly GetMaxOrderingId Instance = new ();

        private GetMaxOrderingId()
        {
        }
    }
}