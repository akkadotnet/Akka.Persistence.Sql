// -----------------------------------------------------------------------
//  <copyright file="Configs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common;
using LinqToDB;

namespace Akka.Persistence.Sql.Tests.Query.Base
{
    public interface ITestConfig
    {
        string TableMapping { get; }
        Database Database { get; }
        string Provider { get; }
        TagMode TagWriteMode { get; }
        TagMode TagReadMode { get; }
    }
    
    internal sealed class SqliteConfig: ITestConfig
    {
        public static readonly SqliteConfig MsTagTable = new (
            Database.MsSqlite, ProviderName.SQLiteMS, TagMode.TagTable, TagMode.TagTable);
        public static readonly SqliteConfig MsCsv = new (
            Database.MsSqlite, ProviderName.SQLiteMS, TagMode.Csv, TagMode.Csv);
        public static readonly SqliteConfig TagTable = new (
            Database.Sqlite, ProviderName.SQLiteClassic, TagMode.TagTable, TagMode.TagTable);
        public static readonly SqliteConfig Csv = new (
            Database.Sqlite, ProviderName.SQLiteClassic, TagMode.Csv, TagMode.Csv);
        
        private SqliteConfig(Database database, string provider, TagMode write, TagMode read)
        {
            Database = database;
            Provider = provider;
            TagWriteMode = write;
            TagReadMode = read;
        }
        
        public string TableMapping => "sqlite";
        public Database Database { get; }
        public string Provider { get; }
        public TagMode TagWriteMode { get; }
        public TagMode TagReadMode { get; }
    }
}
