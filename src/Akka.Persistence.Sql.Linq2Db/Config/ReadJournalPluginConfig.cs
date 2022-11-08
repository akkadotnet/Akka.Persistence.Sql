namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class ReadJournalPluginConfig
    {
        public ReadJournalPluginConfig(Configuration.Config config)
        {
            TagSeparator = config.GetString("tag-separator", ",");
            Dao = config.GetString("dao", "Akka.Persistence.Sql.Linq2Db.Journal.DAO.ByteArrayJournalDao, Akka.Persistence.Sql.Linq2Db");
        }

        public string Dao { get; set; }

        public string TagSeparator { get; set; }
    }
}