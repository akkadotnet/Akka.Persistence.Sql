namespace Akka.Persistence.Sql.Config
{
    public class SnapshotPluginConfig
    {
        public SnapshotPluginConfig(Configuration.Config config)
        {
            Dao = config.GetString("dao", "Akka.Persistence.Sql.Snapshot.ByteArraySnapshotDao, Akka.Persistence.Sql");
        }

        public string Dao { get; }
    }
}
