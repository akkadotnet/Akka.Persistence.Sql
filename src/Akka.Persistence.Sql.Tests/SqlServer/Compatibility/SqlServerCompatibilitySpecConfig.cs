// -----------------------------------------------------------------------
//  <copyright file="SqlServerCompatibilitySpecConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Snapshot;
using Akka.Persistence.Sql.Tests.Common;
using LinqToDB;

namespace Akka.Persistence.Sql.Tests.SqlServer.Compatibility
{
    public class SqlServerCompatibilitySpecConfig
    {
        public static Configuration.Config InitSnapshotConfig(
            TestFixture fixture,
            string tableName)
            => $@"
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

                        sql {{
                            class = ""{typeof(SqlSnapshotStore).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            connection-string = ""{fixture.ConnectionString(Database.SqlServer)}""
                            provider-name = ""{ProviderName.SqlServer2017}""
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

        public static Configuration.Config InitJournalConfig(
            TestFixture fixture,
            string tableName,
            string metadataTableName)
            => $@"
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

                        sql {{
                            class = ""{typeof(SqlWriteJournal).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            connection-string = ""{fixture.ConnectionString(Database.SqlServer)}""
                            provider-name = ""{ProviderName.SqlServer2017}""
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
    }
}
