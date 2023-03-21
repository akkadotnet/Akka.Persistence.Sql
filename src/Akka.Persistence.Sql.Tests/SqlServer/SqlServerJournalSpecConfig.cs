// -----------------------------------------------------------------------
//  <copyright file="SqlServerJournalSpecConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Snapshot;
using LinqToDB;

namespace Akka.Persistence.Sql.Tests.SqlServer
{
    public static class SqlServerJournalSpecConfig
    {
        public static Configuration.Config Create(
            string connString,
            string tableName,
            int batchSize = 100,
            int parallelism = 2)
            => @$"
                akka.persistence {{
                    publish-plugin-commands = on
                    journal {{
                        plugin = ""akka.persistence.journal.sql""
                        sql {{
                            class = ""{typeof(SqlWriteJournal).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            connection-string = ""{connString}""
                            provider-name = ""{ProviderName.SqlServer2017}""
                            parallelism = {parallelism}
                            batch-size = {batchSize}
                            auto-initialize = true
                        }}
                    }}
                }}";
    }

    public static class SqlServerSnapshotSpecConfig
    {
        public static Configuration.Config Create(
            string connString,
            string tableName,
            int batchSize = 100,
            int parallelism = 2)
            => @$"
                akka.persistence {{
                    publish-plugin-commands = on
                    snapshot-store {{
                        plugin = ""akka.persistence.snapshot-store.sql""
                        sql {{
                            class = ""{typeof(SqlSnapshotStore).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            connection-string = ""{connString}""
                            provider-name = ""{ProviderName.SqlServer2017}""
                            parallelism = {parallelism}
                            batch-size = {batchSize}
                            #use-clone-connection = true
                            auto-initialize = true
                            warn-on-auto-init-fail = false
                            default {{
                                journal {{
                                    table-name = ""{tableName}""
                                }}
                            }}
                        }}
                    }}
                }}";
    }
}
