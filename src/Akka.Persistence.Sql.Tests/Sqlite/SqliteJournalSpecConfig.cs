// -----------------------------------------------------------------------
//  <copyright file="SqliteJournalSpecConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common.Containers;
using FluentAssertions.Extensions;
using LinqToDB;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    public static class SqliteSnapshotSpecConfig
    {
        public static Configuration.Config Create(ITestContainer fixture)
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
                            parallelism = 1
                            max-row-by-row-size = 100
                            auto-initialize = true
                            use-clone-connection = {(fixture.ProviderName == ProviderName.SQLiteMS ? "on" : "off")}
                        }}
                    }}
                }}")
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }
    }

    public static class SqliteJournalSpecConfig
    {
        public static Configuration.Config Create(
            ITestContainer fixture,
            bool nativeMode = false)
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
                            parallelism = 1
                            max-row-by-row-size = 100
                            delete-compatibility-mode = {(nativeMode == false ? "on" : "off")}
                            auto-initialize = true
                            use-clone-connection = {(fixture.ProviderName == ProviderName.SQLiteMS ? "on" : "off")}
                        }}
                    }}
                }}")
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }
    }
}
