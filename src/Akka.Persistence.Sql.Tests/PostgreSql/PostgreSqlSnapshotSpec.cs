// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Persistence.Sql.Snapshot;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Snapshot;
using FluentAssertions.Extensions;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.PostgreSql
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(PostgreSqlPersistenceSpec))]
    public class PostgreSqlSnapshotSpec : SnapshotStoreSpec
    {
        private readonly PostgreSqlContainer _fixture;

        public PostgreSqlSnapshotSpec(
            ITestOutputHelper output,
            PostgreSqlContainer fixture) :
            base(
                Configuration(fixture),
                nameof(PostgreSqlSnapshotSpec),
                output)
        {
            _fixture = fixture;
            Initialize();
        }

        protected override void AfterAll()
        {
            base.AfterAll();
            Shutdown();
            if (!_fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
        }

        private static Configuration.Config Configuration(PostgreSqlContainer fixture)
            => @$"
                akka.persistence {{
                    publish-plugin-commands = on
                    snapshot-store {{
                        plugin = ""akka.persistence.snapshot-store.sql""
                        sql {{
                            class = ""{typeof(SqlSnapshotStore).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            connection-string = ""{fixture.ConnectionString}""
                            provider-name = ""{ProviderName.PostgreSQL95}""
                            use-clone-connection = true
                            auto-initialize = true
                            warn-on-auto-init-fail = false
                            default {{
                                journal {{
                                    table-name = l2dbSnapshotSpec
                                }}
                            }}
                        }}
                    }}
                }}";
    }
}
