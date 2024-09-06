// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlSnapshotStoreSaveSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.PostgreSql
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(PostgreSqlPersistenceSpec))]
    public class PostgreSqlSnapshotStoreSaveSnapshotSpec: SnapshotStoreSaveSnapshotSpecBase
    {
        public PostgreSqlSnapshotStoreSaveSnapshotSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(Configuration(fixture), nameof(PostgreSqlSnapshotStoreSaveSnapshotSpec), output)
        {
        }

        private static Configuration.Config Configuration(PostgreSqlContainer fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            return ConfigurationFactory.ParseString(
                    $$"""
                      akka.persistence {
                          publish-plugin-commands = on
                          snapshot-store {
                              plugin = "akka.persistence.snapshot-store.sql"
                              sql {
                                  connection-string = "{{fixture.ConnectionString}}"
                                  provider-name = "{{fixture.ProviderName}}"
                              }
                          }
                      }
                      """)
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }
    }
}
