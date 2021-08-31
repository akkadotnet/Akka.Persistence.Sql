namespace Akka.Persistence.Sql.Linq2Db.Journal.DAO
{
    public enum FlowControlEnum
    {
        Unknown,
        Continue,
        Stop,
        ContinueDelayed
    }
}