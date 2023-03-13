namespace Akka.Persistence.Sql.Config
{
    public class JournalConfig : IProviderConfig<JournalTableConfig>
    {
        public JournalConfig(Configuration.Config config)
        {
            MaterializerDispatcher = config.GetString("materializer-dispatcher","akka.actor.default-dispatcher");
            ConnectionString = config.GetString("connection-string");
            ProviderName = config.GetString("provider-name");
            TableConfig = new JournalTableConfig(config);
            PluginConfig = new JournalPluginConfig(config);
            DaoConfig = new BaseByteArrayJournalDaoConfig(config);
            var dbConf = config.GetString(ConfigKeys.useSharedDb);
            UseSharedDb = string.IsNullOrWhiteSpace(dbConf) ? null : dbConf;
            UseCloneConnection = config.GetBoolean("use-clone-connection", false);
            DefaultSerializer = config.GetString("serializer", null);
            AutoInitialize = config.GetBoolean("auto-initialize");
            WarnOnAutoInitializeFail = config.GetBoolean("warn-on-auto-init-fail");
        }

        public string MaterializerDispatcher { get; }

        public string UseSharedDb { get; }

        public BaseByteArrayJournalDaoConfig DaoConfig { get; }

        public IDaoConfig IDaoConfig => DaoConfig;

        public JournalPluginConfig PluginConfig { get; }

        public JournalTableConfig TableConfig { get; }

        public string DefaultSerializer { get; }

        public string ProviderName { get; }

        public string ConnectionString { get; }

        public bool UseCloneConnection { get; }

        /// <summary>
        /// Flag determining in in case of event journal or metadata table missing, they should be automatically initialized.
        /// </summary>
        public bool AutoInitialize { get; }

        public bool WarnOnAutoInitializeFail { get; }
    }

    public interface IProviderConfig<TTable>
    {
        string ProviderName { get; }

        string ConnectionString { get; }

        TTable TableConfig { get; }

        IDaoConfig IDaoConfig { get; }

        bool UseCloneConnection { get; }

        string DefaultSerializer { get; }
    }

    public interface IDaoConfig
    {
        bool SqlCommonCompatibilityMode { get; }

        int Parallelism { get; }
    }
}
