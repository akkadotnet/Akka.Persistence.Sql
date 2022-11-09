using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Snapshot;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;
using Akka.Persistence.TCK.Journal;
using Akka.Persistence.TCK.Snapshot;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.Postgres
{
    [Collection("PostgreSQLSpec")]
    
    public class PostgreSQLSnapshotSpec : SnapshotStoreSpec
    {
        public static string _snapshotBaseConfig = @"
            akka.persistence {{
                publish-plugin-commands = on
                snapshot-store {{
                    plugin = ""akka.persistence.snapshot-store.linq2db""
                    linq2db {{
                        class = ""{0}""
                        #plugin-dispatcher = ""akka.actor.default-dispatcher""
                        plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                                
                        connection-string = ""{1}""
                        #connection-string = ""FullUri=file:test.db&cache=shared""
                        provider-name = """ + LinqToDB.ProviderName.PostgreSQL95 + @"""
                        use-clone-connection = true
                        tables.snapshot {{ 
                           auto-init = true
                           warn-on-auto-init-fail = false
                           table-name = ""{2}"" 
                        }}
                    }}
                }}
            }}
        ";
        
        
        //private static
        static Configuration.Config conf(PostgreSQLFixture fixture) => ConfigurationFactory.ParseString(
            string.Format(_snapshotBaseConfig,
                typeof(Linq2DbSnapshotStore).AssemblyQualifiedName,fixture.ConnectionString
                ,"l2dbsnapshotSpec"));

        public PostgreSQLSnapshotSpec(ITestOutputHelper outputHelper, PostgreSQLFixture fixture) :
            base(conf(fixture))
        {
            var extension = Linq2DbPersistence.Get(Sys);
            DebuggingHelpers.SetupTraceDump(outputHelper);
            var connFactory = new AkkaPersistenceDataConnectionFactory(
                new SnapshotConfig(
                    conf(fixture)
                        .WithFallback(extension.DefaultConfig)
                        .GetConfig("akka.persistence.snapshot-store.linq2db")));
            using (var conn = connFactory.GetConnection())
            {
                
                try
                {
                    conn.GetTable<SnapshotRow>().Delete();
                }
                catch (Exception e)
                {

                }
            }
            
            Initialize();
        }
    }
    
    [Collection("PostgreSQLSpec")]
    public class DockerLinq2DbPostgreSQLJournalSpec : JournalSpec
    {
        public static string _journalBaseConfig = @"
            akka.persistence {{
                publish-plugin-commands = on
                journal {{
                    plugin = ""akka.persistence.journal.linq2db""
                    linq2db {{
                        class = ""{0}""
                        #plugin-dispatcher = ""akka.actor.default-dispatcher""
                        plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                                
                        connection-string = ""{1}""
                        #connection-string = ""FullUri=file:test.db&cache=shared""
                        provider-name = """ + LinqToDB.ProviderName.PostgreSQL95 + @"""
                        use-clone-connection = true
                        tables.journal {{ 
                           auto-init = true
                           warn-on-auto-init-fail = false
                           table-name = ""{2}"" 
                           }}
                    }}
                }}
            }}
        ";
        
        public static Configuration.Config Create(string connString)
        {
            return ConfigurationFactory.ParseString(
                string.Format(_journalBaseConfig,
                    typeof(Linq2DbWriteJournal).AssemblyQualifiedName,
                    connString,"testJournal"));
        }
        public DockerLinq2DbPostgreSQLJournalSpec(ITestOutputHelper output,
            PostgreSQLFixture fixture) : base(InitConfig(fixture),
            "postgresperf", output)
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

            Initialize();
        }
            
        public static Configuration.Config InitConfig(PostgreSQLFixture fixture)
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

        protected override bool SupportsSerialization => false;
    
    }
}