namespace Akka.Persistence.Sql.Config
{
    public class JournalPluginConfig
    {
        public JournalPluginConfig(Configuration.Config config)
        {
            TagSeparator = config.GetString("tag-separator", ";");
            Dao = config.GetString("dao", "Akka.Persistence.Sql.Journal.Dao.ByteArrayJournalDao, Akka.Persistence.Sql");
        }

        public string TagSeparator { get; }

        public string Dao { get; }
    }
}
