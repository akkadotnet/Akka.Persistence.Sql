// -----------------------------------------------------------------------
//  <copyright file="Linq2DbJournalDefaultSpecConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Tests
{
    public static class Linq2DbJournalDefaultSpecConfig
    {
        public static string CustomConfig(
            string customJournalName,
            string journalTableName,
            string metadataTableName,
            string providerName,
            string connectionString)
            => $@"
akka.persistence.journal.{customJournalName} {{
    class = ""Akka.Persistence.Sql.Journal.Linq2DbWriteJournal, Akka.Persistence.Sql""
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
            string connectionString)
            => $@"
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
            => CustomConfig(
                configName,
                journalTableName,
                metadataTableName,
                providerName,
                connectionString) + (asDefault
                ? $@"
akka{{
  persistence {{
    journal {{
      plugin = akka.persistence.journal.{configName}
    }}
  }}
}}"
                : string.Empty);

        public static Configuration.Config GetConfig(
            string tableName,
            string metadataTableName,
            string providerName,
            string connectionString)
            => JournalBaseConfig(
                tableName,
                metadataTableName,
                providerName,
                connectionString);
    }
}
