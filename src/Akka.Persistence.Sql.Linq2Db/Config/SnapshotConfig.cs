using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Snapshot;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class SnapshotDaoConfig : IDaoConfig
    {
        public SnapshotDaoConfig(bool sqlCommonCompatibilityMode)
        {
            SqlCommonCompatibilityMode = sqlCommonCompatibilityMode;
        }
        public bool SqlCommonCompatibilityMode { get; }
        public int Parallelism { get; } = 0;
    }

    public class SnapshotConfig : IProviderConfig<SnapshotTableConfiguration>
    {
        public SnapshotConfig(Configuration.Config config)
        {
            TableConfig = new SnapshotTableConfiguration(config);
            PluginConfig = new SnapshotPluginConfig(config);
            var dbConf = config.GetString(ConfigKeys.useSharedDb);
            UseSharedDb = string.IsNullOrWhiteSpace(dbConf) ? null : dbConf;
            DefaultSerializer = config.GetString("serializer", null);
            ConnectionString = config.GetString("connection-string", null);
            ProviderName = config.GetString("provider-name", null);
            IDaoConfig = new SnapshotDaoConfig(config.GetBoolean("compatibility-mode", false));
            UseCloneConnection = config.GetBoolean("use-clone-connection", false);
            AutoInitialize = config.GetBoolean("auto-initialize");
            WarnOnAutoInitializeFail = config.GetBoolean("warn-on-auto-init-fail");
        }

        public string ProviderName { get; }

        public string ConnectionString { get; }

        public SnapshotTableConfiguration TableConfig { get; }

        public IDaoConfig IDaoConfig { get; }

        public bool UseCloneConnection { get; }

        public string DefaultSerializer { get; }

        public string UseSharedDb { get; }

        public SnapshotPluginConfig PluginConfig { get; }

        /// <summary>
        /// Flag determining in in case of event journal or metadata table missing, they should be automatically initialized.
        /// </summary>
        public bool AutoInitialize { get; }

        public bool WarnOnAutoInitializeFail { get; }
    }
}