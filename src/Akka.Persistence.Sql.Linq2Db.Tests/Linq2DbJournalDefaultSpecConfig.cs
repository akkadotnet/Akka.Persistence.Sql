using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Journal;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public static class Linq2DbJournalDefaultSpecConfig
    {
        public static string CustomConfig(
            string customJournalName,
            string journalTableName, 
            string metadataTableName,
            string providerName,
            string connectionString) => $@"
akka.persistence.journal.{customJournalName} {{
    class = ""Akka.Persistence.Sql.Linq2Db.Journal.Linq2DbWriteJournal, Akka.Persistence.Sql.Linq2Db""
    provider-name = ""{providerName}""
    connection-string = ""{connectionString}""
    auto-initialize = true
    default {{
        journal {{
            table-name = ""{journalTableName}""
        }}
        metadata {{
            table-name = ""{metadataTableName}""
        }}
    }}
}}";
        
        public static string JournalBaseConfig(
            string tableName,
            string metadataTableName,
            string providerName,
            string connectionString) => $@"
akka.persistence.journal.linq2db {{
    provider-name = ""{providerName}""
    connection-string = ""{connectionString}""
    auto-initialize = true
    default {{
        journal {{
            table-name = ""{tableName}""
        }}
        metadata {{
            table-name = ""{metadataTableName}""
        }}
    }}
}}";

        public static Configuration.Config GetCustomConfig(
            string configName,
            string journalTableName,
            string metadataTableName, 
            string providerName,
            string connectionString, 
            bool asDefault)
        {
            return ConfigurationFactory.ParseString(
                CustomConfig(
                    configName,
                    journalTableName, 
                    metadataTableName, 
                    providerName,
                    connectionString) + (asDefault ? $@"
akka{{
  persistence {{
    journal {{
      plugin = akka.persistence.journal.{configName}
    }}      
  }}
}}" : ""));
        }

        public static Configuration.Config GetConfig(
            string tableName,
            string metadataTableName,
            string providerName,
            string connectionString)
        {
            return ConfigurationFactory
                .ParseString(JournalBaseConfig(tableName, metadataTableName, providerName, connectionString));
        }
    }
}