// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlSpecSettings.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.PostgreSql
{
    public sealed class PostgreSqlSpecSettings: TestSettings
    {
        public static readonly PostgreSqlSpecSettings Instance = new PostgreSqlSpecSettings();

        private PostgreSqlSpecSettings()
        {
        }
        
        public override string JournalTableName => "event_journal";
        
        public override string JournalMetadataTableName => "metadata";
        
        public override string SnapshotTableName => "snapshot_store";
        
        public override string ProviderName => LinqToDB.ProviderName.PostgreSQL;
        
        public override string CompatibilityMode => "postgres";

    }
}