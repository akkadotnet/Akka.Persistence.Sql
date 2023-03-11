namespace Akka.Persistence.Sql.Journal.Dao
{
    public enum FlowControlEnum
    {
        Unknown,
        Continue,
        Stop,
        ContinueDelayed
    }
}
