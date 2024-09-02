// -----------------------------------------------------------------------
//  <copyright file="ReadJournalConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Data;
using Akka.Persistence.Sql.Extensions;
using LinqToDB;

namespace Akka.Persistence.Sql.Config
{
    public class ReadJournalConfig : IProviderConfig<JournalTableConfig>
    {
        public ReadJournalConfig(Configuration.Config config)
        {
            ConnectionString = config.GetString("connection-string");
            ProviderName = config.GetString("provider-name");
            WritePluginId = config.GetString("write-plugin");
            TableConfig = new JournalTableConfig(config);
            DaoConfig = new BaseByteArrayJournalDaoConfig(config);
            UseCloneConnection = config.GetBoolean("use-clone-connection");
            JournalSequenceRetrievalConfiguration = new JournalSequenceRetrievalConfig(config);
            PluginConfig = new ReadJournalPluginConfig(config);
            RefreshInterval = config.GetTimeSpan("refresh-interval", TimeSpan.FromSeconds(1));
            MaxBufferSize = config.GetInt("max-buffer-size", 500);
            AddShutdownHook = config.GetBoolean("add-shutdown-hook", true);
            DefaultSerializer = config.GetString("serializer");
            ReadIsolationLevel = config.GetIsolationLevel("read-isolation-level");
            DataOptions = null;

            // We don't do any writes in a read journal
            WriteIsolationLevel = IsolationLevel.Unspecified;
        }

        private ReadJournalConfig(
            string connectionString,
            string providerName,
            string writePluginId,
            JournalTableConfig tableConfig,
            BaseByteArrayJournalDaoConfig daoConfig,
            int maxBufferSize,
            bool addShutdownHook,
            TimeSpan refreshInterval,
            JournalSequenceRetrievalConfig journalSequenceRetrievalConfiguration,
            IPluginConfig pluginConfig,
            string defaultSerializer,
            IsolationLevel writeIsolationLevel,
            IsolationLevel readIsolationLevel,
            DataOptions? dataOptions,
            bool useCloneConnection)
        {
            ConnectionString = connectionString;
            ProviderName = providerName;
            WritePluginId = writePluginId;
            TableConfig = tableConfig;
            DaoConfig = daoConfig;
            MaxBufferSize = maxBufferSize;
            AddShutdownHook = addShutdownHook;
            RefreshInterval = refreshInterval;
            JournalSequenceRetrievalConfiguration = journalSequenceRetrievalConfiguration;
            PluginConfig = pluginConfig;
            DefaultSerializer = defaultSerializer;
            WriteIsolationLevel = writeIsolationLevel;
            ReadIsolationLevel = readIsolationLevel;
            DataOptions = dataOptions;
            UseCloneConnection = useCloneConnection;
        }

        public string WritePluginId { get; }
        
        public BaseByteArrayJournalDaoConfig DaoConfig { get; }

        public int MaxBufferSize { get; }

        public bool AddShutdownHook { get; }

        public TimeSpan RefreshInterval { get; }

        public JournalSequenceRetrievalConfig JournalSequenceRetrievalConfiguration { get; }

        public IPluginConfig PluginConfig { get; }

        public string ProviderName { get; }

        public string ConnectionString { get; }

        public JournalTableConfig TableConfig { get; }

        public IDaoConfig IDaoConfig => DaoConfig;

        public bool UseCloneConnection { get; }

        public string DefaultSerializer { get; }

        public IsolationLevel WriteIsolationLevel { get; }

        public IsolationLevel ReadIsolationLevel { get; }

        public DataOptions? DataOptions { get; }

        public ReadJournalConfig WithDataOptions(DataOptions dataOptions)
            => Copy(dataOptions: dataOptions);

        private ReadJournalConfig Copy(
            string? connectionString = null,
            string? providerName = null,
            string? writePluginId = null,
            JournalTableConfig? tableConfig = null,
            BaseByteArrayJournalDaoConfig? daoConfig = null,
            int? maxBufferSize = null,
            bool? addShutdownHook = null,
            TimeSpan? refreshInterval = null,
            JournalSequenceRetrievalConfig? journalSequenceRetrievalConfiguration = null,
            IPluginConfig? pluginConfig = null,
            string? defaultSerializer = null,
            IsolationLevel? writeIsolationLevel = null,
            IsolationLevel? readIsolationLevel = null,
            DataOptions? dataOptions = null,
            bool? useCloneConnection = null)
            => new(
                connectionString ?? ConnectionString,
                providerName ?? ProviderName,
                writePluginId ?? WritePluginId,
                tableConfig ?? TableConfig,
                daoConfig ?? DaoConfig,
                maxBufferSize ?? MaxBufferSize,
                addShutdownHook ?? AddShutdownHook,
                refreshInterval ?? RefreshInterval,
                journalSequenceRetrievalConfiguration ?? JournalSequenceRetrievalConfiguration,
                pluginConfig ?? PluginConfig,
                defaultSerializer ?? DefaultSerializer,
                writeIsolationLevel ?? WriteIsolationLevel,
                readIsolationLevel ?? ReadIsolationLevel,
                dataOptions ?? DataOptions,
                useCloneConnection ?? UseCloneConnection);
    }
}
