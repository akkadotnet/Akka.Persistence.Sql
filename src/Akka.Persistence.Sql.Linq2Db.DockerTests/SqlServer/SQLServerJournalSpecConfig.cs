using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Snapshot;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.SqlServer
{
    public static class SqlServerJournalSpecConfig
    {
        public static Configuration.Config Create(string connString, string tableName, int batchSize = 100, int parallelism = 2)
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
            provider-name = ""{LinqToDB.ProviderName.SqlServer2017}""
            parallelism = {parallelism}
            batch-size = {batchSize}
            auto-initialize = true
        }}
    }}
}}");
        }
    }
    
    public static class SqlServerSnapshotSpecConfig
    {
        public static Configuration.Config Create(string connString, string tableName, int batchSize = 100, int parallelism = 2)
        {
            return ConfigurationFactory.ParseString(@$"
akka.persistence {{
    publish-plugin-commands = on
    snapshot-store {{
        plugin = ""akka.persistence.snapshot-store.linq2db""
        linq2db {{
            class = ""{typeof(Linq2DbSnapshotStore).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            connection-string = ""{connString}""
            provider-name = ""{LinqToDB.ProviderName.SqlServer2017}""
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
}}");
        }
    }
}