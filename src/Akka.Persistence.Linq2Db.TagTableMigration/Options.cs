// -----------------------------------------------------------------------
//  <copyright file="Options.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Configuration;

namespace Akka.Persistence.Linq2Db.TagTableMigration;

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
    SqlServer2000,
    SqlServer2005,
    SqlServer2008,
    SqlServer2012,
    SqlServer2014,
    SqlServer2016,
    SqlServer2017,
    MySql,
    MySqlOfficial,
    MySqlConnector,
    PostgreSql,
    PostgreSql92,
    PostgreSql93,
    PostgreSql95,
    Sqlite,
    SqliteClassic,
    SqliteMs
}

public static class Extensions
{
    public static string ToHocon(this DatabaseType db)
        => db switch
        {
            DatabaseType.SqlServer => "sql-server",
            DatabaseType.Sqlite => "sqlite",
            DatabaseType.PostgreSql => "postgresql",
            DatabaseType.MySql => "mysql",
            _ => throw new ArgumentException($"Unknown database type: {db}")
        };

    public static string ToHocon(this ProviderType type)
        => type switch
        {
            ProviderType.SqlServer => LinqToDB.ProviderName.SqlServer,
            ProviderType.SqlServer2000 => LinqToDB.ProviderName.SqlServer2000,
            ProviderType.SqlServer2005 => LinqToDB.ProviderName.SqlServer2005,
            ProviderType.SqlServer2008 => LinqToDB.ProviderName.SqlServer2008,
            ProviderType.SqlServer2012 => LinqToDB.ProviderName.SqlServer2012,
            ProviderType.SqlServer2014 => LinqToDB.ProviderName.SqlServer2014,
            ProviderType.SqlServer2016 => LinqToDB.ProviderName.SqlServer2016,
            ProviderType.SqlServer2017 => LinqToDB.ProviderName.SqlServer2017,
            ProviderType.MySql => LinqToDB.ProviderName.MySql,
            ProviderType.MySqlOfficial => LinqToDB.ProviderName.MySqlOfficial,
            ProviderType.MySqlConnector => LinqToDB.ProviderName.MySqlConnector,
            ProviderType.PostgreSql => LinqToDB.ProviderName.PostgreSQL,
            ProviderType.PostgreSql92 => LinqToDB.ProviderName.PostgreSQL92,
            ProviderType.PostgreSql93 => LinqToDB.ProviderName.PostgreSQL93,
            ProviderType.PostgreSql95 => LinqToDB.ProviderName.PostgreSQL95,
            ProviderType.Sqlite => LinqToDB.ProviderName.SQLite,
            ProviderType.SqliteClassic => LinqToDB.ProviderName.SQLiteClassic,
            ProviderType.SqliteMs => LinqToDB.ProviderName.SQLiteMS,
            _ => throw new ArgumentException($"Unknown provider type: {type}")
        };

    public static Config ToHocon(this Options opt)
        => (Config)$@"
akka.persistence {{
	journal {{
		plugin = ""akka.persistence.journal.linq2db""
		linq2db {{
			connection-string = ""{opt.ConnectionString}""
			provider-name = {opt.Provider.ToHocon()}
			table-mapping = {opt.TableMapping.ToHocon()}
            auto-initialize = off
            warn-on-auto-init-fail = true
            tag-write-mode = Both

{(opt.SchemaName is { } ? @$"
            {opt.TableMapping.ToHocon()} {{
                schema-name = {opt.SchemaName}
            }}" : "")}
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