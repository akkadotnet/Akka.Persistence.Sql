// -----------------------------------------------------------------------
//  <copyright file="SqlJournalDefaultSpecConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Configuration;

namespace Akka.Persistence.Sql.Tests
{
    public static class SqlJournalDefaultSpecConfig
    {
        private static string CustomConfig(
            string customJournalName,
            string journalTableName,
            string metadataTableName,
            string providerName,
            string connectionString)
            => $@"
akka.persistence.journal.{customJournalName} {{
    class = ""Akka.Persistence.Sql.Journal.SqlWriteJournal, Akka.Persistence.Sql""
    provider-name = ""{providerName}""
    connection-string = ""{connectionString}""
    default {{
        journal {{
            table-name = ""{journalTableName}""
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
                ? $"akka.persistence.journal.plugin = akka.persistence.journal.{configName}"
                : string.Empty);

        public static Configuration.Config GetDefaultConfig(
            string providerName,
            string connectionString)
            => ConfigurationFactory.ParseString(
                    $@"
akka.persistence.journal {{
    plugin = akka.persistence.journal.sql
    sql {{
        provider-name = ""{providerName}""
        connection-string = ""{connectionString}""
    }}
}}")
                .WithFallback(SqlPersistence.DefaultConfiguration);
    }
}
