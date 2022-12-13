using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Snapshot;
using Akka.Persistence.Sql.Linq2Db.Tests;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;

namespace Akka.Persistence.Linq2Db.CompatibilityTests.Docker.SqlServer
{
    public class SqlServerCompatibilitySpecConfig
    {
        public static Config InitSnapshotConfig(string tableName)
        {
            DbUtils.ConnectionString = DockerDbUtils.ConnectionString;
            var specString = $@"
akka.persistence {{
    publish-plugin-commands = on
    snapshot-store {{
		sql-server {{
			class = ""Akka.Persistence.SqlServer.Snapshot.SqlServerSnapshotStore, Akka.Persistence.SqlServer""
			plugin-dispatcher = ""akka.actor.default-dispatcher""
			connection-string = ""{DbUtils.ConnectionString}""
			connection-timeout = 30s
			schema-name = dbo
			table-name = ""{tableName}""
			auto-initialize = on

			sequential-access = off
		}}
	
        linq2db {{
            class = ""{typeof(Linq2DbSnapshotStore).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            connection-string = ""{DbUtils.ConnectionString}""
            provider-name = ""{LinqToDB.ProviderName.SqlServer2017}""
            table-mapping = sql-server
            auto-initialize = true
            sql-server {{
                snapshot {{
                    table-name = ""{tableName}"" 
                }}
            }}
        }}
    }}
}}";

            return ConfigurationFactory.ParseString(specString);
        }
        
        public static Config InitJournalConfig(string tableName, string metadataTableName)
        {
            DbUtils.ConnectionString = DockerDbUtils.ConnectionString;
            var specString = $@"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.sql-server""

        sql-server {{
            class = ""Akka.Persistence.SqlServer.Journal.SqlServerJournal, Akka.Persistence.SqlServer""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            table-name = ""{tableName}""
            metadata-table-name = ""{metadataTableName}""
            schema-name = dbo
            auto-initialize = on
            connection-string = ""{DbUtils.ConnectionString}""
        }}

        linq2db {{
            class = ""{typeof(Linq2DbWriteJournal).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            connection-string = ""{DbUtils.ConnectionString}""
            provider-name = ""{LinqToDB.ProviderName.SqlServer2017}""
            parallelism = 3
            table-mapping = sql-server
            auto-initialize = true
            sql-server {{
                journal {{
                    table-name = ""{tableName}"" 
                }}
                metadata {{
                    table-name = ""{metadataTableName}"" 
                }}
            }}
        }}
    }}
}}";

            return ConfigurationFactory.ParseString(specString);
        }
    }
}