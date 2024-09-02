// -----------------------------------------------------------------------
//  <copyright file="SnapshotConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Data;
using Akka.Persistence.Sql.Extensions;
using LinqToDB;

namespace Akka.Persistence.Sql.Config
{
    public class SnapshotDaoConfig : IDaoConfig
    {
        public SnapshotDaoConfig(bool sqlCommonCompatibilityMode)
            => SqlCommonCompatibilityMode = sqlCommonCompatibilityMode;

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
            UseSharedDb = string.IsNullOrWhiteSpace(dbConf)
                ? null
                : dbConf;

            DefaultSerializer = config.GetString("serializer");
            ConnectionString = config.GetString("connection-string");
            ProviderName = config.GetString("provider-name");
            IDaoConfig = new SnapshotDaoConfig(config.GetBoolean("compatibility-mode"));
            UseCloneConnection = config.GetBoolean("use-clone-connection");
            AutoInitialize = config.GetBoolean("auto-initialize");
            WarnOnAutoInitializeFail = config.GetBoolean("warn-on-auto-init-fail");
            ReadIsolationLevel = config.GetIsolationLevel("read-isolation-level");
            WriteIsolationLevel = config.GetIsolationLevel("write-isolation-level");
            DataOptions = null;
        }

        public SnapshotConfig(
            SnapshotTableConfiguration tableConfig,
            IPluginConfig pluginConfig,
            string? useSharedDb,
            string defaultSerializer,
            string connectionString,
            string providerName,
            IDaoConfig daoConfig,
            bool useCloneConnection,
            bool autoInitialize,
            bool warnOnAutoInitializeFail,
            IsolationLevel writeIsolationLevel,
            IsolationLevel readIsolationLevel,
            DataOptions? dataOptions)
        {
            TableConfig = tableConfig;
            PluginConfig = pluginConfig;
            UseSharedDb = useSharedDb;
            DefaultSerializer = defaultSerializer;
            ConnectionString = connectionString;
            ProviderName = providerName;
            IDaoConfig = daoConfig;
            UseCloneConnection = useCloneConnection;
            AutoInitialize = autoInitialize;
            WarnOnAutoInitializeFail = warnOnAutoInitializeFail;
            WriteIsolationLevel = writeIsolationLevel;
            ReadIsolationLevel = readIsolationLevel;
            DataOptions = dataOptions;
        }

        public string? UseSharedDb { get; }

        /// <summary>
        ///     Flag determining in in case of event journal or metadata table missing, they should be automatically initialized.
        /// </summary>
        public bool AutoInitialize { get; }

        public bool WarnOnAutoInitializeFail { get; }

        public IPluginConfig PluginConfig { get; }

        public string ProviderName { get; }

        public string ConnectionString { get; }

        public SnapshotTableConfiguration TableConfig { get; }

        public IDaoConfig IDaoConfig { get; }

        public bool UseCloneConnection { get; }

        public string DefaultSerializer { get; }

        public DataOptions? DataOptions { get; }

        public IsolationLevel WriteIsolationLevel { get; }

        public IsolationLevel ReadIsolationLevel { get; }

        public SnapshotConfig WithDataOptions(DataOptions dataOptions)
            => Copy(dataOptions: dataOptions);

        private SnapshotConfig Copy(
            SnapshotTableConfiguration? tableConfig = null,
            IPluginConfig? pluginConfig = null,
            string? useSharedDb = null,
            string? defaultSerializer = null,
            string? connectionString = null,
            string? providerName = null,
            IDaoConfig? daoConfig = null,
            bool? useCloneConnection = false,
            bool? autoInitialize = false,
            bool? warnOnAutoInitializeFail = false,
            IsolationLevel? writeIsolationLevel = null,
            IsolationLevel? readIsolationLevel = null,
            DataOptions? dataOptions = null)
            => new(
                tableConfig ?? TableConfig,
                pluginConfig ?? PluginConfig,
                useSharedDb ?? UseSharedDb,
                defaultSerializer ?? DefaultSerializer,
                connectionString ?? ConnectionString,
                providerName ?? ProviderName,
                daoConfig ?? IDaoConfig,
                useCloneConnection ?? UseCloneConnection,
                autoInitialize ?? AutoInitialize,
                warnOnAutoInitializeFail ?? WarnOnAutoInitializeFail,
                writeIsolationLevel ?? WriteIsolationLevel,
                readIsolationLevel ?? ReadIsolationLevel,
                dataOptions ?? DataOptions);
    }
}
