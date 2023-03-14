// -----------------------------------------------------------------------
//  <copyright file="SqliteJournalSpecConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Snapshot;
using LinqToDB;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    public static class SqliteSnapshotSpecConfig
    {
        public static Configuration.Config Create(
            string connString,
            string providerName)
            => ConfigurationFactory.ParseString(
                $@"
                akka.persistence {{
                    publish-plugin-commands = on
                    snapshot-store {{
                        plugin = ""akka.persistence.snapshot-store.linq2db""
                        linq2db {{
                            class = ""{typeof(Linq2DbSnapshotStore).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            connection-string = ""{connString}""
                            provider-name = ""{providerName}""
                            parallelism = 1
                            max-row-by-row-size = 100
                            auto-initialize = true
                            warn-on-auto-init-fail = false
                            use-clone-connection = {(providerName == ProviderName.SQLiteMS ? "on" : "off")}
                        }}
                    }}
                }}");
    }

    public static class SqliteJournalSpecConfig
    {
        public static Configuration.Config Create(
            string connString,
            string providerName,
            bool nativeMode = false)
            => ConfigurationFactory.ParseString(
                $@"
                akka.persistence {{
                    publish-plugin-commands = on
                    journal {{
                        plugin = ""akka.persistence.journal.linq2db""
                        linq2db {{
                            class = ""{typeof(Linq2DbWriteJournal).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            connection-string = ""{connString}""
                            provider-name = ""{providerName}""
                            parallelism = 1
                            max-row-by-row-size = 100
                            delete-compatibility-mode = {(nativeMode == false ? "on" : "off")}
                            auto-initialize = true
                            use-clone-connection = {(providerName == ProviderName.SQLiteMS ? "on" : "off")}
                        }}
                    }}
                }}");
    }
}
