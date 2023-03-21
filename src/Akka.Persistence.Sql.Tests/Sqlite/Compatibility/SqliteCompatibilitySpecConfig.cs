// -----------------------------------------------------------------------
//  <copyright file="SqliteCompatibilitySpecConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Snapshot;
using LinqToDB;

namespace Akka.Persistence.Sql.Tests.Sqlite.Compatibility
{
    public class SqliteCompatibilitySpecConfig
    {
        public static Configuration.Config InitSnapshotConfig(string tableName, string connectionString)
            => ConfigurationFactory.ParseString(
                    $@"
                    akka.persistence {{
	                    publish-plugin-commands = on
	                    snapshot-store {{
		                    sqlite {{
			                    class = ""Akka.Persistence.Sqlite.Snapshot.SqliteSnapshotStore, Akka.Persistence.Sqlite""
			                    plugin-dispatcher = ""akka.actor.default-dispatcher""
			                    connection-string = ""{connectionString}""
			                    connection-timeout = 30s
			                    schema-name = dbo
			                    table-name = ""{tableName}""
			                    auto-initialize = on
		                    }}

		                    sql {{
			                    class = ""{typeof(SqlSnapshotStore).AssemblyQualifiedName}""
                                plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
			                    connection-string = ""{connectionString}""
			                    provider-name = ""{ProviderName.SQLiteMS}""
			                    table-mapping = sqlite
                                auto-initialize = true
                                sqlite {{
                                    snapshot {{
                                        table-name = ""{tableName}""
                                    }}
                                }}
		                    }}
	                    }}
                    }}")
                .WithFallback(SqlPersistence.DefaultConfiguration);

        public static Configuration.Config InitJournalConfig(
            string tableName,
            string metadataTableName,
            string connectionString)
            => $@"
                akka.persistence {{
	                publish-plugin-commands = on
	                journal {{
		                plugin = ""akka.persistence.journal.sqlite""
		                sqlite {{
			                class = ""Akka.Persistence.Sqlite.Journal.SqliteJournal, Akka.Persistence.Sqlite""
			                plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
			                table-name = ""{tableName}""
			                metadata-table-name = ""{metadataTableName}""
			                schema-name = dbo
			                auto-initialize = on
			                connection-string = ""{connectionString}""
		                }}
		                sql {{
			                class = ""{typeof(SqlWriteJournal).AssemblyQualifiedName}""
			                plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
			                connection-string = ""{connectionString}""
			                provider-name = ""{ProviderName.SQLiteMS}""
			                parallelism = 3
                            table-mapping = sqlite
                            tag-write-mode = Csv
                            auto-initialize = true
                            sqlite {{
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
    }
}
