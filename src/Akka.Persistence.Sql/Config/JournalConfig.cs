// -----------------------------------------------------------------------
//  <copyright file="JournalConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Data;
using Akka.Persistence.Sql.Extensions;
using LinqToDB;

namespace Akka.Persistence.Sql.Config
{
    public class JournalConfig : IProviderConfig<JournalTableConfig>
    {
        public JournalConfig(Configuration.Config config)
        {
            MaterializerDispatcher = config.GetString("materializer-dispatcher", "akka.actor.default-dispatcher");
            ConnectionString = config.GetString("connection-string");
            ProviderName = config.GetString("provider-name");
            TableConfig = new JournalTableConfig(config);
            PluginConfig = new JournalPluginConfig(config);
            DaoConfig = new BaseByteArrayJournalDaoConfig(config);

            var dbConf = config.GetString(ConfigKeys.useSharedDb);
            UseSharedDb = string.IsNullOrWhiteSpace(dbConf)
                ? null
                : dbConf;

            UseCloneConnection = config.GetBoolean("use-clone-connection");
            DefaultSerializer = config.GetString("serializer");
            AutoInitialize = config.GetBoolean("auto-initialize");
            WarnOnAutoInitializeFail = config.GetBoolean("warn-on-auto-init-fail");

            ReadIsolationLevel = config.GetIsolationLevel("read-isolation-level");
            WriteIsolationLevel = config.GetIsolationLevel("write-isolation-level");

            DataOptions = null;
        }

        private JournalConfig(
            string materializerDispatcher,
            string connectionString,
            string providerName,
            JournalTableConfig tableConfig,
            IPluginConfig pluginConfig,
            BaseByteArrayJournalDaoConfig daoConfig,
            string? useSharedDb,
            bool useCloneConnection,
            string defaultSerializer,
            bool autoInitialize,
            bool warnOnAutoInitializeFail,
            IsolationLevel writeIsolationLevel,
            IsolationLevel readIsolationLevel,
            DataOptions? dataOptions)
        {
            MaterializerDispatcher = materializerDispatcher;
            ConnectionString = connectionString;
            ProviderName = providerName;
            TableConfig = tableConfig;
            PluginConfig = pluginConfig;
            DaoConfig = daoConfig;
            UseSharedDb = useSharedDb;
            UseCloneConnection = useCloneConnection;
            DefaultSerializer = defaultSerializer;
            AutoInitialize = autoInitialize;
            WarnOnAutoInitializeFail = warnOnAutoInitializeFail;
            WriteIsolationLevel = writeIsolationLevel;
            ReadIsolationLevel = readIsolationLevel;
            DataOptions = dataOptions;
        }

        public string MaterializerDispatcher { get; }

        public string? UseSharedDb { get; }

        public BaseByteArrayJournalDaoConfig DaoConfig { get; }

        /// <summary>
        ///     Flag determining in in case of event journal or metadata table missing, they should be automatically initialized.
        /// </summary>
        public bool AutoInitialize { get; }

        public bool WarnOnAutoInitializeFail { get; }

        public IPluginConfig PluginConfig { get; }

        public IDaoConfig IDaoConfig => DaoConfig;

        public JournalTableConfig TableConfig { get; }

        public string DefaultSerializer { get; }

        public string ProviderName { get; }

        public string ConnectionString { get; }

        public bool UseCloneConnection { get; }

        public IsolationLevel WriteIsolationLevel { get; }

        public IsolationLevel ReadIsolationLevel { get; }

        public DataOptions? DataOptions { get; }

        public JournalConfig WithDataOptions(DataOptions dataOptions)
            => Copy(dataOptions: dataOptions);

        private JournalConfig Copy(
            string? materializerDispatcher = null,
            string? connectionString = null,
            string? providerName = null,
            JournalTableConfig? tableConfig = null,
            IPluginConfig? pluginConfig = null,
            BaseByteArrayJournalDaoConfig? daoConfig = null,
            string? useSharedDb = null,
            bool? useCloneConnection = null,
            string? defaultSerializer = null,
            bool? autoInitialize = null,
            bool? warnOnAutoInitializeFail = null,
            IsolationLevel? writeIsolationLevel = null,
            IsolationLevel? readIsolationLevel = null,
            DataOptions? dataOptions = null)
            => new(
                materializerDispatcher ?? MaterializerDispatcher,
                connectionString ?? ConnectionString,
                providerName ?? ProviderName,
                tableConfig ?? TableConfig,
                pluginConfig ?? PluginConfig,
                daoConfig ?? DaoConfig,
                useSharedDb ?? UseSharedDb,
                useCloneConnection ?? UseCloneConnection,
                defaultSerializer ?? DefaultSerializer,
                autoInitialize ?? AutoInitialize,
                warnOnAutoInitializeFail ?? WarnOnAutoInitializeFail,
                writeIsolationLevel ?? WriteIsolationLevel,
                readIsolationLevel ?? ReadIsolationLevel,
                dataOptions ?? DataOptions);
    }

    public interface IProviderConfig
    {
        IsolationLevel WriteIsolationLevel { get; }

        IsolationLevel ReadIsolationLevel { get; }
    }

    public interface IProviderConfig<TTable> : IProviderConfig
    {
        string ProviderName { get; }

        string ConnectionString { get; }

        TTable TableConfig { get; }

        IPluginConfig PluginConfig { get; }

        IDaoConfig IDaoConfig { get; }

        bool UseCloneConnection { get; }

        string DefaultSerializer { get; }

        DataOptions? DataOptions { get; }
    }

    public interface IPluginConfig
    {
        // ReSharper disable once InconsistentNaming
        string TagSeparator { get; }

        string Dao { get; }

        // ReSharper disable once InconsistentNaming
        TagMode TagMode { get; }
    }

    public interface IDaoConfig
    {
        bool SqlCommonCompatibilityMode { get; }

        int Parallelism { get; }
    }
}
