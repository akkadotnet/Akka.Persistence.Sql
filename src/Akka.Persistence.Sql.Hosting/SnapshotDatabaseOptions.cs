// -----------------------------------------------------------------------
//  <copyright file="SnapshotDatabaseOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Text;
using Akka.Hosting;

namespace Akka.Persistence.Sql.Hosting
{
    public sealed class SnapshotDatabaseOptions
    {
        public SnapshotDatabaseOptions(DatabaseMapping mapping)
            => Mapping = mapping;

        public static SnapshotDatabaseOptions Default => new(DatabaseMapping.Default)
        {
            SnapshotTable = SnapshotTableOptions.Default,
        };

        public static SnapshotDatabaseOptions SqlServer => new(DatabaseMapping.SqlServer)
        {
            SnapshotTable = SnapshotTableOptions.SqlServer,
        };

        public static SnapshotDatabaseOptions Sqlite => new(DatabaseMapping.Sqlite)
        {
            SnapshotTable = SnapshotTableOptions.Sqlite,
        };

        public static SnapshotDatabaseOptions PostgreSql => new(DatabaseMapping.PostgreSql)
        {
            SnapshotTable = SnapshotTableOptions.PostgreSql,
        };

        public static SnapshotDatabaseOptions MySql => new(DatabaseMapping.MySql)
        {
            SnapshotTable = SnapshotTableOptions.MySql,
        };

        public DatabaseMapping Mapping { get; }

        /// <summary>
        ///     <para>
        ///         SQL schema name to table corresponding with persistent snapshot.
        ///     </para>
        ///     <b>Default</b>: <c>null</c>
        /// </summary>
        public string? SchemaName { get; set; }

        /// <summary>
        ///     Snapshot store table column name mapping
        /// </summary>
        public SnapshotTableOptions? SnapshotTable { get; set; }

        internal void Build(StringBuilder psb)
        {
            var sb = new StringBuilder();
            if (SchemaName is { })
                sb.AppendLine($"schema-name = {SchemaName.ToHocon()}");

            SnapshotTable?.Build(sb);

            if (sb.Length > 0)
            {
                psb.AppendLine($"{Mapping.Name()} {{{Environment.NewLine}");
                psb.Append(sb);
                psb.AppendLine("}");
            }
        }
    }
}
