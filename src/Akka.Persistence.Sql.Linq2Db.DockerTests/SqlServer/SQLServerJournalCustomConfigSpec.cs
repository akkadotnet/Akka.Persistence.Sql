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
    public class SqlServerJournalCustomConfigSpec : JournalSpec
    {
        public static Configuration.Config Initialize(SqlServerFixture fixture)
        {
            DockerDbUtils.Initialize(fixture.ConnectionString);
            return Configuration;
        }

        private static Configuration.Config Configuration =>
            Linq2DbJournalDefaultSpecConfig.GetCustomConfig(
                "customSpec",
                "customJournalSpec", 
                "customJournalMetadata",
                ProviderName.SqlServer2017, DockerDbUtils.ConnectionString, true);
        
        public SqlServerJournalCustomConfigSpec(ITestOutputHelper outputHelper, SqlServerFixture fixture)
            : base(Initialize(fixture), "SQLServer-custom", outputHelper)
        {
            var extension = Linq2DbPersistence.Get(Sys);
            var connFactory = new AkkaPersistenceDataConnectionFactory(new JournalConfig(
                Configuration
                    .GetConfig("akka.persistence.journal.customSpec")
                    .WithFallback(extension.DefaultJournalConfig)));
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