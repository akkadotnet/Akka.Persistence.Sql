namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class SnapshotPluginConfig
    {
        public SnapshotPluginConfig(Configuration.Config config)
        {
            Dao = config.GetString("dao", "Akka.Persistence.Sql.Linq2Db.Snapshot.ByteArraySnapshotDao, Akka.Persistence.Sql.Linq2Db");
        }

        public string Dao { get; }
    }
}