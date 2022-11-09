using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Snapshot;
using LinqToDB;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public static class SQLiteSnapshotSpecConfig
    {
        public static string _journalBaseConfig = @"
            
        ";
        
        public static Configuration.Config Create(string connString, string providerName)
        {
            return ConfigurationFactory.ParseString($@"
akka.persistence {{
    publish-plugin-commands = on
    snapshot-store {{
        plugin = ""akka.persistence.snapshot-store.testspec""
        testspec {{
            class = ""{typeof(Linq2DbSnapshotStore).AssemblyQualifiedName}""
            #plugin-dispatcher = ""akka.actor.default-dispatcher""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            connection-string = ""{connString}""
            #connection-string = ""FullUri=file:test.db&cache=shared""
            provider-name = ""{providerName}""
            parallelism = 1
            max-row-by-row-size = 100
            tables.snapshot {{ 
                auto-init = true 
                warn-on-auto-init-fail = false
            }}
            use-clone-connection = {(providerName == ProviderName.SQLiteMS ? "on" : "off")}
        }}
    }}
}}");
        }
    }
    public static class SQLiteJournalSpecConfig
    {
        public static Configuration.Config Create(string connString, string providerName, bool nativeMode = false)
        {
            return ConfigurationFactory.ParseString($@"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.linq2db""
        linq2db {{
            class = ""{typeof(Linq2DbWriteJournal).AssemblyQualifiedName}""
            #plugin-dispatcher = ""akka.actor.default-dispatcher""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            connection-string = ""{connString}""
            #connection-string = ""FullUri=file:test.db&cache=shared""
            provider-name = ""{providerName}""
            parallelism = 1
            max-row-by-row-size = 100
            delete-compatibility-mode = {(nativeMode == false ? "on" : "off")}
            tables.journal {{ 
                auto-init = true
                warn-on-auto-init-fail = false 
            }}
            use-clone-connection = {(providerName == ProviderName.SQLiteMS ? "on" : "off")}
        }}
    }}
}}");
        }
    }
}