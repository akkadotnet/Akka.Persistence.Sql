﻿// -----------------------------------------------------------------------
//  <copyright file="Configs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using LinqToDB;

namespace Akka.Persistence.Sql.Tests.Common.Query
{
    public interface ITestConfig
    {
        Database Database { get; }
        string Provider { get; }
        TagMode TagMode { get; }
    }
    
    public sealed class SqliteConfig: ITestConfig
    {
        public static readonly SqliteConfig MsTagTable = new (
            Database.MsSqlite, ProviderName.SQLiteMS, TagMode.TagTable);
        public static readonly SqliteConfig MsCsv = new (
            Database.MsSqlite, ProviderName.SQLiteMS, TagMode.Csv);
        public static readonly SqliteConfig TagTable = new (
            Database.Sqlite, ProviderName.SQLiteClassic, TagMode.TagTable);
        public static readonly SqliteConfig Csv = new (
            Database.Sqlite, ProviderName.SQLiteClassic, TagMode.Csv);
        
        private SqliteConfig(Database database, string provider, TagMode mode)
        {
            Database = database;
            Provider = provider;
            TagMode = mode;
        }
        
        public Database Database { get; }
        public string Provider { get; }
        public TagMode TagMode { get; }
    }

    public sealed class PostgreSqlConfig : ITestConfig
    {
        public static readonly PostgreSqlConfig TagTable = new (TagMode.TagTable);
        public static readonly PostgreSqlConfig Csv = new (TagMode.Csv);

        private PostgreSqlConfig(TagMode mode)
        {
            TagMode = mode;
        }
        
        public Database Database => Database.PostgreSql;
        public string Provider => ProviderName.PostgreSQL95;
        public TagMode TagMode { get; }
    }

    public sealed class SqlServerConfig : ITestConfig
    {
        public static readonly SqlServerConfig TagTable = new (TagMode.TagTable);
        public static readonly SqlServerConfig Csv = new (TagMode.Csv);

        private SqlServerConfig(TagMode mode)
        {
            TagMode = mode;
        }
        
        public Database Database => Database.SqlServer;
        public string Provider => ProviderName.SqlServer2017;
        public TagMode TagMode { get; }
    }
}