namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public sealed class ScheduleAssumeMaxOrderingId
    {
        public ScheduleAssumeMaxOrderingId(long maxInDatabase)
        {
            MaxInDatabase = maxInDatabase;
        }

        public long MaxInDatabase { get; }
    }
}
