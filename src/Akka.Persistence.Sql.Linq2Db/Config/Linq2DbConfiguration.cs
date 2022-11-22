namespace Akka.Persistence.Sql.Linq2Db.Config
{
    
    public class Linq2DbConfiguration
    {
        public Linq2DbConfiguration(Configuration.Config config)
        {
            ProviderName = config.GetString("provider-name");
            ConnectionString = config.GetString("connection-string");
        }

        public string ConnectionString { get; }

        public string ProviderName { get; }
    }
}