namespace Akka.Persistence.Sql.Linq2Db.Journal.Dao
{
    public enum FlowControlEnum
    {
        Unknown,
        Continue,
        Stop,
        ContinueDelayed
    }
}