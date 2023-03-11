// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Snapshot;
using Akka.Persistence.TCK.Snapshot;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.PostgreSql
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class PostgreSqlSnapshotSpec : SnapshotStoreSpec, IAsyncLifetime
    {
        private static Configuration.Config Configuration(TestFixture fixture) =>
            ConfigurationFactory.ParseString(@$"
akka.persistence {{
    publish-plugin-commands = on
    snapshot-store {{
        plugin = ""akka.persistence.snapshot-store.linq2db""
        linq2db {{
            class = ""{typeof(Linq2DbSnapshotStore).AssemblyQualifiedName}""
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
}}");

        private readonly TestFixture _fixture;

        public PostgreSqlSnapshotSpec(ITestOutputHelper output, TestFixture fixture) :
            base(Configuration(fixture), nameof(PostgreSqlSnapshotSpec), output)
        {
            _fixture = fixture;
            //DebuggingHelpers.SetupTraceDump(output);
        }

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.PostgreSql);
            Initialize();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
