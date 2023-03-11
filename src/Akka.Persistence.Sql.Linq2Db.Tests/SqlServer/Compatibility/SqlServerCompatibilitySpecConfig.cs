using Akka.Configuration;
using Akka.Persistence.Linq2Db.Tests.Common;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Snapshot;

namespace Akka.Persistence.Sql.Linq2Db.Tests.SqlServer.Compatibility
{
    public class SqlServerCompatibilitySpecConfig
    {
        public static Configuration.Config InitSnapshotConfig(TestFixture fixture, string tableName)
        {
            var specString = $@"
akka.persistence {{
    publish-plugin-commands = on
    snapshot-store {{
		sql-server {{
			class = ""Akka.Persistence.SqlServer.Snapshot.SqlServerSnapshotStore, Akka.Persistence.SqlServer""
			plugin-dispatcher = ""akka.actor.default-dispatcher""
			connection-string = ""{fixture.ConnectionString(Database.SqlServer)}""
			connection-timeout = 30s
			schema-name = dbo
			table-name = ""{tableName}""
			auto-initialize = on

			sequential-access = off
		}}

        linq2db {{
            class = ""{typeof(Linq2DbSnapshotStore).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            connection-string = ""{fixture.ConnectionString(Database.SqlServer)}""
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

        public static Configuration.Config InitJournalConfig(TestFixture fixture, string tableName, string metadataTableName)
        {
            var specString = $@"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        sql-server {{
            class = ""Akka.Persistence.SqlServer.Journal.SqlServerJournal, Akka.Persistence.SqlServer""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            table-name = ""{tableName}""
            metadata-table-name = ""{metadataTableName}""
            schema-name = dbo
            auto-initialize = on
            connection-string = ""{fixture.ConnectionString(Database.SqlServer)}""
        }}

        linq2db {{
            class = ""{typeof(Linq2DbWriteJournal).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            connection-string = ""{fixture.ConnectionString(Database.SqlServer)}""
            provider-name = ""{LinqToDB.ProviderName.SqlServer2017}""
            parallelism = 3
            table-mapping = sql-server
            tag-write-mode = Csv
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