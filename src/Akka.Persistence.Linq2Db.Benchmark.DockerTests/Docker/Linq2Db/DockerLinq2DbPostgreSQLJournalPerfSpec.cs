using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.BenchmarkTests.Docker.Linq2Db
{
    [Collection("PostgreSQLSpec")]
    public class DockerLinq2DbPostgreSQLJournalPerfSpec : L2dbJournalPerfSpec
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
            #plugin-dispatcher = ""akka.actor.default-dispatcher""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""

            connection-string = ""{connString}""
            #connection-string = ""FullUri=file:test.db&cache=shared""
            provider-name = ""{ProviderName.PostgreSQL95}""
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
        
        public DockerLinq2DbPostgreSQLJournalPerfSpec(ITestOutputHelper output,
            PostgreSQLFixture fixture) : base(InitConfig(fixture),
            "postgresperf", output,40, eventsCount: TestConstants.DockerNumMessages)
        {
            var extension = Linq2DbPersistence.Get(Sys);
            var config = Create(DockerDbUtils.ConnectionString)
                .WithFallback(extension.DefaultConfig)
                .GetConfig("akka.persistence.journal.linq2db");
            var connFactory = new AkkaPersistenceDataConnectionFactory(new JournalConfig(config));
            using (var conn = connFactory.GetConnection())
            {
                try
                {
                    conn.GetTable<JournalRow>().Delete();
                }
                catch (Exception e)
                {
                }
                
            }
        }
            
        public static Config InitConfig(PostgreSQLFixture fixture)
        {
            //need to make sure db is created before the tests start
            //DbUtils.Initialize(fixture.ConnectionString);
            

            return Create(fixture.ConnectionString);
        }  
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
//            DbUtils.Clean();
        }

        [Fact]
        public void PersistenceActor_Must_measure_PersistGroup1000()
        {
            RunGroupBenchmark(1000,10);
        }
    }
}