// -----------------------------------------------------------------------
//  <copyright file="JournalDatabaseOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Text;
using Akka.Hosting;

namespace Akka.Persistence.Sql.Hosting
{
    public sealed class JournalDatabaseOptions
    {
        public JournalDatabaseOptions(DatabaseMapping mapping)
            => Mapping = mapping;

        public static JournalDatabaseOptions Default => new(DatabaseMapping.Default)
        {
            JournalTable = JournalTableOptions.Default,
            MetadataTable = MetadataTableOptions.Default,
            TagTable = TagTableOptions.Default,
        };

        public static JournalDatabaseOptions SqlServer => new(DatabaseMapping.SqlServer)
        {
            SchemaName = "dbo",
            JournalTable = JournalTableOptions.SqlServer,
            MetadataTable = MetadataTableOptions.SqlServer,
        };

        public static JournalDatabaseOptions Sqlite => new(DatabaseMapping.Sqlite)
        {
            JournalTable = JournalTableOptions.Sqlite,
            MetadataTable = MetadataTableOptions.Sqlite,
        };

        public static JournalDatabaseOptions PostgreSql => new(DatabaseMapping.PostgreSql)
        {
            SchemaName = "public",
            JournalTable = JournalTableOptions.PostgreSql,
            MetadataTable = MetadataTableOptions.PostgreSql,
        };

        public static JournalDatabaseOptions MySql => new(DatabaseMapping.MySql)
        {
            JournalTable = JournalTableOptions.MySql,
            MetadataTable = MetadataTableOptions.MySql,
        };

        public DatabaseMapping Mapping { get; }

        /// <summary>
        ///     <para>
        ///         SQL schema name to table corresponding with persistent journal.
        ///     </para>
        ///     <b>Default</b>: <c>null</c>
        /// </summary>
        public string? SchemaName { get; set; }

        /// <summary>
        ///     Journal events table column name mapping
        /// </summary>
        public JournalTableOptions? JournalTable { get; set; }

        /// <summary>
        ///     Journal metadata table column name mapping, if metadata table is being used
        /// </summary>
        public MetadataTableOptions? MetadataTable { get; set; }

        /// <summary>
        ///     Event tag table column name mapping, if tag table is being used
        /// </summary>
        public TagTableOptions? TagTable { get; set; }

        internal void Build(StringBuilder psb)
        {
            var sb = new StringBuilder();
            if (SchemaName is not null)
                sb.AppendLine($"schema-name = {SchemaName.ToHocon()}");

            JournalTable?.Build(sb);
            MetadataTable?.Build(sb);
            TagTable?.Build(sb);

            if (sb.Length > 0)
            {
                psb.AppendLine($"{Mapping.Name()} {{{Environment.NewLine}");
                psb.Append(sb);
                psb.AppendLine("}");
            }
        }
    }
}
