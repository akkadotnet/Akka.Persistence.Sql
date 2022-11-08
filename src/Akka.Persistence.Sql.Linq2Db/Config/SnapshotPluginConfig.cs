namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class SnapshotPluginConfig
    {
        public SnapshotPluginConfig(Configuration.Config config)
        {
            Dao = config.GetString("dao", "Akka.Persistence.Sql.Linq2Db.Journal.DAO.ByteArrayJournalDao, Akka.Persistence.Sql.Linq2Db");
        }

        public string Dao { get; protected set; }
    }
}