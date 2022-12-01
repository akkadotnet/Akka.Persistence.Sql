using System;
using System.Collections.Generic;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Tests;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.BenchmarkTests.Docker.Linq2Db
{
    
    [Collection("SqlServerSpec")]
    public class DockerLinq2DbSqlServerJournalPerfSpec : L2dbJournalPerfSpec
    {
        private static Config Create(string connString)
        {
            return ConfigurationFactory.ParseString($@"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.linq2db""
        linq2db {{
            class = ""{typeof(Linq2DbWriteJournal).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""

            connection-string = ""{connString}""
            provider-name = ""{ProviderName.SqlServer2017}""
            use-clone-connection = true
            tables.journal {{ 
                auto-init = true
                warn-on-auto-init-fail = false
                table-name = ""testPerfTable"" 
            }}
        }}
    }}
}}");
        }
        
        public DockerLinq2DbSqlServerJournalPerfSpec(ITestOutputHelper output,
            SqlServerFixture fixture) : base(InitConfig(fixture),
            "sqlserverperf", output,40, eventsCount: TestConstants.DockerNumMessages)
        {
            var extension = Linq2DbPersistence.Get(Sys);
            var config = Create(SqlServerDbUtils.ConnectionString)
                .WithFallback(extension.DefaultConfig)
                .GetConfig("akka.persistence.journal.linq2db");
            var connFactory = new AkkaPersistenceDataConnectionFactory(new JournalConfig(config));
            using var conn = connFactory.GetConnection();
            try
            {
                conn.GetTable<JournalRow>().Delete();
            }
            catch
            {
                // no-op
            }
        }
            
        public static Config InitConfig(SqlServerFixture fixture)
        {
            //need to make sure db is created before the tests start
            SqlServerDbUtils.Initialize(fixture.ConnectionString);

            return Create(SqlServerDbUtils.ConnectionString);
        }  
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            SqlServerDbUtils.Clean();
        }

        [Fact]
        public void PersistenceActor_Must_measure_PersistGroup1000()
        {
            RunGroupBenchmark(1000,10);
        }
    }
}