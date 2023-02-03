using System;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;
using Akka.Persistence.TCK.Journal;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.SqlServer
{
    [Collection("SqlServerSpec")]
    public class SqlServerJournalDefaultConfigSpec : JournalSpec
    {
        public static Configuration.Config Initialize(SqlServerFixture fixture)
        {
            SqlServerDbUtils.Initialize(fixture.ConnectionString);
            return Configuration;
        }
        
        private static Configuration.Config Configuration =>
            Linq2DbJournalDefaultSpecConfig.GetConfig(
                "defaultJournalSpec", 
                "defaultJournalMetadata", 
                ProviderName.SqlServer2017,
                SqlServerDbUtils.ConnectionString);
        
        public SqlServerJournalDefaultConfigSpec(ITestOutputHelper outputHelper, SqlServerFixture fixture)
            : base(Initialize(fixture), "SQLServer-default", outputHelper)
        {
            var connFactory = new AkkaPersistenceDataConnectionFactory(new JournalConfig(
                Configuration
                    .WithFallback(Linq2DbPersistence.DefaultConfiguration)
                    .GetConfig("akka.persistence.journal.linq2db")));
            using (var conn = connFactory.GetConnection())
            {
                try
                {
                    conn.GetTable<JournalRow>().Delete();
                }
                catch
                {
                   // no-op
                }
                try
                {
                    conn.GetTable<JournalMetaData>().Delete();
                }
                catch
                {
                   // no-op
                }
            }

            Initialize();
        }
        
        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;
    }
}