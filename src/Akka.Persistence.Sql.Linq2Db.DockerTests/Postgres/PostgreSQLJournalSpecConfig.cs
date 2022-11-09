using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Journal;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.Postgres
{
    public static class PostgreSQLJournalSpecConfig
    {
        public static string _journalBaseConfig = @"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.linq2db""
        linq2db {{
            class = ""{0}""
            #plugin-dispatcher = ""akka.actor.default-dispatcher""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            connection-string = ""{1}""
            provider-name = ""{2}""
            use-clone-connection = false
            tables.journal {{ 
                auto-init = true 
                warn-on-auto-init-fail = false
            }}
        }}
    }}
}}";
        
        public static Configuration.Config Create(string connString, string providerName)
        {
            return ConfigurationFactory.ParseString(
                string.Format(_journalBaseConfig,
                    typeof(Linq2DbWriteJournal).AssemblyQualifiedName,
                    connString, providerName));
        }
    }
}