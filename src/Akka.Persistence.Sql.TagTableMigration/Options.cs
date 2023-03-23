// -----------------------------------------------------------------------
//  <copyright file="Options.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using LinqToDB;

namespace Akka.Persistence.Sql.TagTableMigration
{
    public enum DatabaseType
    {
        SqlServer,
        Sqlite,
        PostgreSql,
        MySql
    }

    public enum ProviderType
    {
        SqlServer,
        SqlServer2005,
        SqlServer2008,
        SqlServer2012,
        SqlServer2014,
        SqlServer2016,
        SqlServer2017,
        SqlServer2019,
        SqlServer2022,
        MySql,
        MySqlOfficial,
        MySqlConnector,
        PostgreSql,
        PostgreSql92,
        PostgreSql93,
        PostgreSql95,
        PostgreSql15,
        Sqlite,
        SqliteClassic,
        SqliteMs
    }

    public static class Extensions
    {
        public static string ToHocon(this DatabaseType databaseType)
            => databaseType switch
            {
                DatabaseType.SqlServer => "sql-server",
                DatabaseType.Sqlite => "sqlite",
                DatabaseType.PostgreSql => "postgresql",
                DatabaseType.MySql => "mysql",
                _ => throw new ArgumentException($"Unknown database type: {databaseType}")
            };

        public static string ToHocon(this ProviderType type)
            => type switch
            {
                ProviderType.SqlServer => ProviderName.SqlServer,
                ProviderType.SqlServer2005 => ProviderName.SqlServer2005,
                ProviderType.SqlServer2008 => ProviderName.SqlServer2008,
                ProviderType.SqlServer2012 => ProviderName.SqlServer2012,
                ProviderType.SqlServer2014 => ProviderName.SqlServer2014,
                ProviderType.SqlServer2016 => ProviderName.SqlServer2016,
                ProviderType.SqlServer2017 => ProviderName.SqlServer2017,
                ProviderType.SqlServer2019 => ProviderName.SqlServer2019,
                ProviderType.SqlServer2022 => ProviderName.SqlServer2022,
                ProviderType.MySql => ProviderName.MySql,
                ProviderType.MySqlOfficial => ProviderName.MySqlOfficial,
                ProviderType.MySqlConnector => ProviderName.MySqlConnector,
                ProviderType.PostgreSql => ProviderName.PostgreSQL,
                ProviderType.PostgreSql92 => ProviderName.PostgreSQL92,
                ProviderType.PostgreSql93 => ProviderName.PostgreSQL93,
                ProviderType.PostgreSql95 => ProviderName.PostgreSQL95,
                ProviderType.PostgreSql15 => ProviderName.PostgreSQL15,
                ProviderType.Sqlite => ProviderName.SQLite,
                ProviderType.SqliteClassic => ProviderName.SQLiteClassic,
                ProviderType.SqliteMs => ProviderName.SQLiteMS,
                _ => throw new ArgumentException($"Unknown provider type: {type}")
            };

        public static Configuration.Config ToHocon(this Options opt)
            => (Configuration.Config)$@"
akka.persistence {{
	journal {{
		plugin = ""akka.persistence.journal.sql""
		sql {{
			connection-string = ""{opt.ConnectionString}""
			provider-name = {opt.Provider.ToHocon()}
			table-mapping = {opt.TableMapping.ToHocon()}
            auto-initialize = off
            warn-on-auto-init-fail = true
            tag-write-mode = Both

{(opt.SchemaName is { } ? @$"
            {opt.TableMapping.ToHocon()} {{
                schema-name = {opt.SchemaName}
            }}" : string.Empty)}
		}}
	}}
}}";
    }

    public sealed class Options
    {
        public string? ConnectionString { get; set; }
        public DatabaseType TableMapping { get; set; }
        public ProviderType Provider { get; set; }
        public string? SchemaName { get; set; }
        public long StartOffset { get; set; }
        public long? EndOffset { get; set; }
        public int BatchSize { get; set; }

        public override string ToString()
            => $"ConnectionString: {ConnectionString}, TableMapping: {TableMapping}, Provider: {Provider}, " +
               $"SchemaName: {SchemaName}, StartOffset: {StartOffset}, EndOffset: {EndOffset}, BatchSize: {BatchSize}";
    }
}
