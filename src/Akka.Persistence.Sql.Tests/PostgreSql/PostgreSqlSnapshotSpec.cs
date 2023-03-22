﻿// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Snapshot;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Snapshot;
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
    [Collection("PersistenceSpec")]
    public class PostgreSqlSnapshotSpec : SnapshotStoreSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public PostgreSqlSnapshotSpec(
            ITestOutputHelper output,
            TestFixture fixture) :
            base(
                Configuration(fixture),
                nameof(PostgreSqlSnapshotSpec),
                output)
            => _fixture = fixture;

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.PostgreSql);
            Initialize();
        }

        public Task DisposeAsync()
            => Task.CompletedTask;

        private static Configuration.Config Configuration(TestFixture fixture)
            => @$"
                akka.persistence {{
                    publish-plugin-commands = on
                    snapshot-store {{
                        plugin = ""akka.persistence.snapshot-store.sql""
                        sql {{
                            class = ""{typeof(SqlSnapshotStore).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            connection-string = ""{fixture.ConnectionString(Database.PostgreSql)}""
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
