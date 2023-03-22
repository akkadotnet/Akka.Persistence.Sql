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
        TagMode TagMode { get; }
    }
    
    internal sealed class SqliteConfig: ITestConfig
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
        
        public string TableMapping => "default";
        public Database Database { get; }
        public string Provider { get; }
        public TagMode TagMode { get; }
    }
}
