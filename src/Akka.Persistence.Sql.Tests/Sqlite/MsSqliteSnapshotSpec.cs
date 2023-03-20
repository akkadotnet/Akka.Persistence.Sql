// -----------------------------------------------------------------------
//  <copyright file="MsSqliteSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Snapshot;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    [Collection("PersistenceSpec")]
    public class MsSqliteSnapshotSpec : SnapshotStoreSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public MsSqliteSnapshotSpec(
            ITestOutputHelper outputHelper,
            TestFixture fixture)
            : base(
                SqliteSnapshotSpecConfig.Create(
                    fixture.ConnectionString(Database.MsSqlite),
                    ProviderName.SQLiteMS),
                nameof(MsSqliteSnapshotSpec),
                outputHelper)
            => _fixture = fixture;

        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.MsSqlite);
            Initialize();
        }

        public Task DisposeAsync()
            => Task.CompletedTask;
    }
}
