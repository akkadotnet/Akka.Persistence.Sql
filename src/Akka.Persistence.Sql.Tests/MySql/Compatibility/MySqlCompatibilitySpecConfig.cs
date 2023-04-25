// -----------------------------------------------------------------------
//  <copyright file="MySqlCompatibilitySpecConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.MySql.Journal;
using Akka.Persistence.MySql.Snapshot;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Snapshot;
using Akka.Persistence.Sql.Tests.Common.Containers;

namespace Akka.Persistence.Sql.Tests.MySql.Compatibility
{
    public static class MySqlCompatibilitySpecConfig
    {
        public static Configuration.Config InitSnapshotConfig(
            MySqlContainer fixture,
            string tableName)
            => $@"
                akka.persistence {{
                    publish-plugin-commands = on
                    snapshot-store {{
                        mysql {{
                            class = ""{typeof(MySqlSnapshotStore).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.actor.default-dispatcher""
                            connection-string = ""{fixture.ConnectionString}""
                            connection-timeout = 30s
                            table-name = {tableName}
                            auto-initialize = on
                            sequential-access = on
                        }}

                        sql {{
                            class = ""{typeof(SqlSnapshotStore).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.actor.default-dispatcher""
                            connection-string = ""{fixture.ConnectionString}""
                            provider-name = {fixture.ProviderName}
                            table-mapping = mysql
                            auto-initialize = true
                            read-isolation-level = read-committed
                            write-isolation-level = read-committed
                            mysql {{
                                snapshot {{
                                    table-name = ""{tableName}""
                                }}
                            }}
                        }}
                    }}
                }}";

        public static Configuration.Config InitJournalConfig(
            MySqlContainer fixture,
            string tableName,
            string metadataTableName)
            => $@"
                akka.persistence {{
                    publish-plugin-commands = on
                    journal {{
                        mysql {{
                            class = ""{typeof(MySqlJournal).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.actor.default-dispatcher""
                            connection-string = ""{fixture.ConnectionString}""
                            connection-timeout = 30s
                            table-name = ""{tableName}""
                            metadata-table-name = ""{metadataTableName}""
                            auto-initialize = on
                        }}

                        sql {{
                            class = ""{typeof(SqlWriteJournal).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            connection-string = ""{fixture.ConnectionString}""
                            provider-name = ""{fixture.ProviderName}""
                            parallelism = 3
                            table-mapping = mysql
                            auto-initialize = true
                            tag-write-mode = Csv
                            delete-compatibility-mode = true
                            read-isolation-level = read-committed
                            write-isolation-level = read-committed
                            mysql {{
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
