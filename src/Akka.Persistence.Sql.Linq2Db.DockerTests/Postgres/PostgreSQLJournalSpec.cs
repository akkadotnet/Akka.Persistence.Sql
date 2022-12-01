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
    
    public class PostgreSqlSnapshotSpec : SnapshotStoreSpec
    {
        private static Configuration.Config Configuration(PostgreSqlFixture fixture) =>
            ConfigurationFactory.ParseString(@$"
akka.persistence {{
    publish-plugin-commands = on
    snapshot-store {{
        plugin = ""akka.persistence.snapshot-store.linq2db""
        linq2db {{
            class = ""{typeof(Linq2DbSnapshotStore).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            connection-string = ""{fixture.ConnectionString}""
            provider-name = ""{ProviderName.PostgreSQL95}""
            use-clone-connection = true
            tables.snapshot {{ 
               auto-init = true
               warn-on-auto-init-fail = false
               table-name = l2dbSnapshotSpec 
            }}
        }}
    }}
}}");

        public PostgreSqlSnapshotSpec(ITestOutputHelper outputHelper, PostgreSqlFixture fixture) :
            base(Configuration(fixture))
        {
            var extension = Linq2DbPersistence.Get(Sys);
            DebuggingHelpers.SetupTraceDump(outputHelper);
            var connFactory = new AkkaPersistenceDataConnectionFactory(
                new SnapshotConfig(
                    Configuration(fixture)
                        .WithFallback(extension.DefaultConfig)
                        .GetConfig("akka.persistence.snapshot-store.linq2db")));
            using (var conn = connFactory.GetConnection())
            {
                try
                {
                    conn.GetTable<SnapshotRow>().Delete();
                }
                catch
                {
                    // no-op
                }
            }
            
            Initialize();
        }
    }
    
    [Collection("PostgreSQLSpec")]
    public class DockerLinq2DbPostgreSqlJournalSpec : JournalSpec
    {
        public static Configuration.Config Create(string connString)
        {
            return ConfigurationFactory.ParseString(@$"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.linq2db""
        linq2db {{
            class = ""{typeof(Linq2DbWriteJournal).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            connection-string = ""{connString}""
            provider-name = ""{ProviderName.PostgreSQL95}""
            use-clone-connection = true
            tables.journal {{ 
               auto-init = true
               warn-on-auto-init-fail = false
               table-name = testJournal 
            }}
        }}
    }}
}}");
        }
        
        public DockerLinq2DbPostgreSqlJournalSpec(ITestOutputHelper output, PostgreSqlFixture fixture) 
            : base(InitConfig(fixture), "postgresperf", output)
        {
            var extension = Linq2DbPersistence.Get(Sys);
            var config = Create(SqlServerDbUtils.ConnectionString)
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
            }

            Initialize();
        }
            
        public static Configuration.Config InitConfig(PostgreSqlFixture fixture)
        {
            return Create(fixture.ConnectionString);
        }  

        protected override bool SupportsSerialization => false;
    
    }
}