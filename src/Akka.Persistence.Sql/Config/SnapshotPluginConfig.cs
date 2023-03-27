// -----------------------------------------------------------------------
//  <copyright file="SnapshotPluginConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Config
{
    public class SnapshotPluginConfig
    {
        public SnapshotPluginConfig(Configuration.Config config)
            => Dao = config.GetString(
                "dao",
                "Akka.Persistence.Sql.Snapshot.ByteArraySnapshotDao, Akka.Persistence.Sql");

        public string Dao { get; }
    }
}
