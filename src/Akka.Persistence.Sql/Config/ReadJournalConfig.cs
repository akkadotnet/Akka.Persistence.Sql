// -----------------------------------------------------------------------
//  <copyright file="ReadJournalConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Data;
using Akka.Persistence.Sql.Extensions;

namespace Akka.Persistence.Sql.Config
{
    public class ReadJournalConfig : IProviderConfig<JournalTableConfig>
    {
        public ReadJournalConfig(Configuration.Config config)
        {
            ConnectionString = config.GetString("connection-string");
            ProviderName = config.GetString("provider-name");
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
            
            // We don't do any writes in a read journal
            WriteIsolationLevel = IsolationLevel.Unspecified;
        }

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
    }
}
