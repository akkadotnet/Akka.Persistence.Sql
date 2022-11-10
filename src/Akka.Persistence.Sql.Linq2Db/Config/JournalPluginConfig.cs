namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class JournalPluginConfig
    {
        public JournalPluginConfig(Configuration.Config config)
        {
            TagSeparator = config.GetString("tag-separator", ";");
            Dao = config.GetString("dao", "Akka.Persistence.Sql.Linq2Db.Journal.DAO.ByteArrayJournalDao, Akka.Persistence.Sql.Linq2Db");
        }
        
        public string TagSeparator { get; }
        
        public string Dao { get; }
    }
}