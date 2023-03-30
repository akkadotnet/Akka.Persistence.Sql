// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlCompatibilitySpecConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.PostgreSql.Snapshot;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Snapshot;
using Akka.Persistence.Sql.Tests.Common.Containers;
using LinqToDB;

namespace Akka.Persistence.Sql.Tests.PostgreSql.Compatibility
{
    public class PostgreSqlCompatibilitySpecConfig
    {
        public static Configuration.Config InitSnapshotConfig(
            PostgreSqlContainer fixture,
            string tableName)
            => $@"
                akka.persistence {{
                    publish-plugin-commands = on
                    snapshot-store {{
		                postgresql {{
			                class = ""{typeof(PostgreSqlSnapshotStore).AssemblyQualifiedName}""
			                plugin-dispatcher = ""akka.actor.default-dispatcher""
			                connection-string = ""{fixture.ConnectionString}""
			                connection-timeout = 30s
			                schema-name = public
			                table-name = {tableName}
			                auto-initialize = on
			                sequential-access = off
		                }}

                        sql {{
                            class = ""{typeof(SqlSnapshotStore).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.actor.default-dispatcher""
                            connection-string = ""{fixture.ConnectionString}""
                            provider-name = {ProviderName.PostgreSQL95}
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

        public static Configuration.Config InitJournalConfig(
            PostgreSqlContainer fixture,
            string tableName,
            string metadataTableName)
            => $@"
                akka.persistence {{
                    publish-plugin-commands = on
                    journal {{
                        postgresql {{
                            class = ""Akka.Persistence.PostgreSql.Journal.PostgreSqlJournal, Akka.Persistence.PostgreSql""
                            plugin-dispatcher = ""akka.actor.default-dispatcher""
                            connection-string = ""{fixture.ConnectionString}""
                            connection-timeout = 30s
                            schema-name = public
                            table-name = ""{tableName}""
                            metadata-table-name = ""{metadataTableName}""
                            auto-initialize = on
                        }}

                        sql {{
                            class = ""{typeof(SqlWriteJournal).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            connection-string = ""{fixture.ConnectionString}""
                            provider-name = ""{ProviderName.PostgreSQL95}""
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
    }
}
