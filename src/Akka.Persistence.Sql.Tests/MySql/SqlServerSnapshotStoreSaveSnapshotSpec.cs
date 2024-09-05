// -----------------------------------------------------------------------
//  <copyright file="MySqlSnapshotStoreSaveSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common.Containers;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.MySql
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(MySqlPersistenceSpec))]
    public class MySqlSnapshotStoreSaveSnapshotSpec: SnapshotStoreSaveSnapshotSpecBase
    {
        public MySqlSnapshotStoreSaveSnapshotSpec(ITestOutputHelper output, MySqlContainer fixture)
            : base(Configuration(fixture), nameof(MySqlSnapshotStoreSaveSnapshotSpec), output)
        {
        }

        private static Configuration.Config Configuration(MySqlContainer fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            return ConfigurationFactory.ParseString(
                    $$"""
                      akka.persistence {
                          publish-plugin-commands = on
                          journal {
                              plugin = "akka.persistence.journal.sql"
                              sql {
                                  connection-string = "{{fixture.ConnectionString}}"
                                  provider-name = "{{fixture.ProviderName}}"
                                  read-isolation-level = read-committed
                                  write-isolation-level = read-committed
                              }
                          }
                          snapshot-store {
                              plugin = "akka.persistence.snapshot-store.sql"
                              sql {
                                  connection-string = "{{fixture.ConnectionString}}"
                                  provider-name = "{{fixture.ProviderName}}"
                                  read-isolation-level = read-committed
                                  write-isolation-level = read-committed
                              }
                          }
                      }
                      """)
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }
    }
}
