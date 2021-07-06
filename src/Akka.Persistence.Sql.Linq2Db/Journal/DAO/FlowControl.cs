namespace Akka.Persistence.Sql.Linq2Db.Journal.DAO
{
    public class FlowControl
    {
        public class Continue : FlowControl
        {
            private Continue()
            {
            }

            public static Continue Instance = new Continue();
        }

        public class ContinueDelayed : FlowControl
        {
            private ContinueDelayed()
            {
            }

            public static ContinueDelayed Instance = new ContinueDelayed();
        }

        public class Stop : FlowControl
        {
            private Stop()
            {
            }

            public static Stop Instance = new Stop();
        }
    }
}