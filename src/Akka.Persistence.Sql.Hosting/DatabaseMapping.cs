// -----------------------------------------------------------------------
//  <copyright file="DatabaseMapping.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Hosting
{
    public enum DatabaseMapping
    {
        Default,
        Sqlite,
        SqlServer,
        PostgreSql,
        MySql,
    }

    public static class DatabaseMappingExtension
    {
        public static string Name(this DatabaseMapping map)
            => map switch
            {
                DatabaseMapping.Default => "default",
                DatabaseMapping.Sqlite => "sqlite",
                DatabaseMapping.SqlServer => "sql-server",
                DatabaseMapping.PostgreSql => "postgresql",
                DatabaseMapping.MySql => "mysql",
                _ => throw new Exception($"Unknown DatabaseMapping: {map}")
            };

        public static JournalDatabaseOptions JournalOption(this DatabaseMapping map)
            => map switch
            {
                DatabaseMapping.Default => JournalDatabaseOptions.Default,
                DatabaseMapping.SqlServer => JournalDatabaseOptions.SqlServer,
                DatabaseMapping.Sqlite => JournalDatabaseOptions.Sqlite,
                DatabaseMapping.PostgreSql => JournalDatabaseOptions.PostgreSql,
                DatabaseMapping.MySql => JournalDatabaseOptions.MySql,
                _ => throw new Exception($"Unknown DatabaseMapping: {map}") 
            };

        public static SnapshotDatabaseOptions SnapshotOption(this DatabaseMapping map)
            => map switch
            {
                DatabaseMapping.Default => SnapshotDatabaseOptions.Default,
                DatabaseMapping.SqlServer => SnapshotDatabaseOptions.SqlServer,
                DatabaseMapping.Sqlite => SnapshotDatabaseOptions.Sqlite,
                DatabaseMapping.PostgreSql => SnapshotDatabaseOptions.PostgreSql,
                DatabaseMapping.MySql => SnapshotDatabaseOptions.MySql,
                _ => throw new Exception($"Unknown DatabaseMapping: {map}") 
            };
    }
}
