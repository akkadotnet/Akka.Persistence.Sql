// -----------------------------------------------------------------------
//  <copyright file="JournalConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Data;
using Akka.Persistence.Sql.Extensions;

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
        }

        public string MaterializerDispatcher { get; }

        public string UseSharedDb { get; }

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
