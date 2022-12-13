using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Journal;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.Postgres
{
    public static class PostgreSqlJournalSpecConfig
    {
        public static Configuration.Config Create(string connString, string providerName)
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
            provider-name = ""{providerName}""
            use-clone-connection = false
            auto-initialize = true
            warn-on-auto-init-fail = false
        }}
    }}
}}");
        }
    }
}