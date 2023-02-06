using Akka.Configuration;
using Akka.Persistence.PostgreSql;
using Akka.Persistence.PostgreSql.Journal;
using Akka.Persistence.PostgreSql.Snapshot;
using Akka.Persistence.Sql.Linq2Db;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Snapshot;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;

namespace Akka.Persistence.Linq2Db.CompatibilityTests.Docker.Postgres
{
    public class PostgreSqlCompatibilitySpecConfig
    {
        public static Config InitSnapshotConfig(string tableName)
        {
            var specString = $@"
akka.persistence {{
    publish-plugin-commands = on
    snapshot-store {{
		postgresql {{
			class = ""{typeof(PostgreSqlSnapshotStore).AssemblyQualifiedName}""
			plugin-dispatcher = ""akka.actor.default-dispatcher""
			connection-string = ""{PostgreDbUtils.ConnectionString}""
			connection-timeout = 30s
			schema-name = public
			table-name = {tableName}
			auto-initialize = on
			sequential-access = off
		}}
	
        linq2db {{
            class = ""{typeof(Linq2DbSnapshotStore).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.actor.default-dispatcher""
            connection-string = ""{PostgreDbUtils.ConnectionString}""
            provider-name = {LinqToDB.ProviderName.PostgreSQL95}
            table-mapping = postgresql
            auto-initialize = true
            postgresql {{
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
            var specString = $@"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        postgresql {{
            class = ""Akka.Persistence.PostgreSql.Journal.PostgreSqlJournal, Akka.Persistence.PostgreSql""
            plugin-dispatcher = ""akka.actor.default-dispatcher""
            connection-string = ""{PostgreDbUtils.ConnectionString}""
            connection-timeout = 30s
            schema-name = public
            table-name = ""{tableName}""
            metadata-table-name = ""{metadataTableName}""
            auto-initialize = on
        }}

        linq2db {{
            class = ""{typeof(Linq2DbWriteJournal).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            connection-string = ""{PostgreDbUtils.ConnectionString}""
            provider-name = ""{LinqToDB.ProviderName.PostgreSQL95}""
            parallelism = 3
            table-mapping = postgresql
            auto-initialize = true
            tag-write-mode = Csv
            postgresql {{
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