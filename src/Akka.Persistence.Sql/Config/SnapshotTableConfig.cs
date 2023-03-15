// -----------------------------------------------------------------------
//  <copyright file="SnapshotTableConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Config
{
    public sealed class SnapshotTableConfig
    {
        public SnapshotTableConfig(Configuration.Config config)
        {
            var snapshotConfig = config.GetConfig("snapshot");
            Name = snapshotConfig.GetString("table-name");
            ColumnNames = new SnapshotTableColumnNames(snapshotConfig);
        }

        public string Name { get; }

        public SnapshotTableColumnNames ColumnNames { get; }
    }
}
