// -----------------------------------------------------------------------
//  <copyright file="SnapshotTableConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Config
{
    public sealed class SnapshotTableConfig: IEquatable<SnapshotTableConfig>
    {
        public SnapshotTableConfig(Configuration.Config config)
        {
            var snapshotConfig = config.GetConfig("snapshot");
            Name = snapshotConfig.GetString("table-name");
            ColumnNames = new SnapshotTableColumnNames(snapshotConfig);
        }

        public string Name { get; }

        public SnapshotTableColumnNames ColumnNames { get; }

        public bool Equals(SnapshotTableConfig? other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return Name == other.Name && ColumnNames.Equals(other.ColumnNames);
        }

        public override bool Equals(object? obj)
            => obj is not null && (ReferenceEquals(this, obj) || obj is SnapshotTableConfig other && Equals(other));

        public override int GetHashCode()
            => HashCode.Combine(Name, ColumnNames);
    }
}
