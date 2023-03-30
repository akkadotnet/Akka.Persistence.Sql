// -----------------------------------------------------------------------
//  <copyright file="SqlServerJournalSpecConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Snapshot;
using Akka.Persistence.Sql.Tests.Common.Containers;
using FluentAssertions.Extensions;

namespace Akka.Persistence.Sql.Tests.SqlServer
{
    public static class SqlServerJournalSpecConfig
    {
        public static Configuration.Config Create(
            SqlServerContainer fixture,
            string tableName,
            int batchSize = 100,
            int parallelism = 2)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
            
            return ConfigurationFactory.ParseString(@$"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.sql""
        sql {{
            connection-string = ""{fixture.ConnectionString}""
            provider-name = ""{fixture.ProviderName}""
            parallelism = {parallelism}
            batch-size = {batchSize}
            auto-initialize = true
            default {{
                journal {{
                    table-name = ""{tableName}""
                }}
            }}
        }}
    }}
}}")
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }
    }

    public static class SqlServerSnapshotSpecConfig
    {
        public static Configuration.Config Create(
            SqlServerContainer fixture,
            string tableName,
            int batchSize = 100,
            int parallelism = 2)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
            
            return ConfigurationFactory.ParseString(@$"
akka.persistence {{
    publish-plugin-commands = on
    snapshot-store {{
        plugin = ""akka.persistence.snapshot-store.sql""
        sql {{
            connection-string = ""{fixture.ConnectionString}""
            provider-name = ""{fixture.ProviderName}""
            parallelism = {parallelism}
            batch-size = {batchSize}
            auto-initialize = true
            default {{
                journal {{
                    table-name = ""{tableName}""
                }}
            }}
        }}
    }}
}}")
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }
    }
}
