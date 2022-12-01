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
    public class SqlServerJournalSpec : JournalSpec
    {
        public static Configuration.Config Initialize(SqlServerFixture fixture)
        {
            SqlServerDbUtils.Initialize(fixture.ConnectionString);
            return Configuration;
        }

        private static Configuration.Config Configuration =>
            SqlServerJournalSpecConfig.Create(SqlServerDbUtils.ConnectionString, "journalSpec");
        
        public SqlServerJournalSpec(ITestOutputHelper outputHelper, SqlServerFixture fixture)
            : base(Initialize(fixture), "SQLServer", outputHelper)
        {
            var extension = Linq2DbPersistence.Get(Sys);
            var config = Configuration
                .WithFallback(extension.DefaultConfig)
                .GetConfig("akka.persistence.journal.linq2db");
            var connFactory = new AkkaPersistenceDataConnectionFactory(new JournalConfig(config));
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