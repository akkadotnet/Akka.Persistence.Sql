// -----------------------------------------------------------------------
//  <copyright file="SqlServerSpecSettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.SqlServer
{
    public sealed class SqlServerSpecSettings: TestSettings
    {
        public static readonly SqlServerSpecSettings Instance = new SqlServerSpecSettings();

        private SqlServerSpecSettings()
        {
        }
        
        public override string JournalTableName => "EventJournal";
        
        public override string JournalMetadataTableName => "Metadata";
        
        public override string SnapshotTableName => "SnapshotStore";
        
        public override string ProviderName => LinqToDB.ProviderName.SqlServer2017;
        
        public override string CompatibilityMode => "sqlserver";

        public override string SchemaName => "dbo";
        
    }
}