// -----------------------------------------------------------------------
//  <copyright file="SqlServerSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Snapshot;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.SqlServer
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class SqlServerSnapshotSpec : SnapshotStoreSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public SqlServerSnapshotSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                Configuration(fixture),
                nameof(SqlServerSnapshotSpec),
                output)
            => _fixture = fixture;

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.SqlServer);
            Initialize();
        }

        public Task DisposeAsync()
            => Task.CompletedTask;

        private static Configuration.Config Configuration(TestFixture fixture)
            => SqlServerSnapshotSpecConfig.Create(
                fixture.ConnectionString(Database.SqlServer),
                "snapshotSpec");
    }
}
