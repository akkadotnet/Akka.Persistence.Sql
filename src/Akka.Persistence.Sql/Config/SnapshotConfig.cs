﻿// -----------------------------------------------------------------------
//  <copyright file="SnapshotConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

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
        }

        public string UseSharedDb { get; }

        public IPluginConfig PluginConfig { get; }

        /// <summary>
        ///     Flag determining in in case of event journal or metadata table missing, they should be automatically initialized.
        /// </summary>
        public bool AutoInitialize { get; }

        public bool WarnOnAutoInitializeFail { get; }

        public string ProviderName { get; }

        public string ConnectionString { get; }

        public SnapshotTableConfiguration TableConfig { get; }

        public IDaoConfig IDaoConfig { get; }

        public bool UseCloneConnection { get; }

        public string DefaultSerializer { get; }
    }
}
