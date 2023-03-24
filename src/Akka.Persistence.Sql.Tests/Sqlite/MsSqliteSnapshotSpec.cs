// -----------------------------------------------------------------------
//  <copyright file="MsSqliteSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Snapshot;
using FluentAssertions.Extensions;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

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

        public Task InitializeAsync()
        {
            Initialize();
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            using var cts = new CancellationTokenSource(10.Seconds());
            await Task.WhenAny(
                Task.Delay(Timeout.Infinite, cts.Token),
                _fixture.InitializeDbAsync(Database.MsSqlite));
            
            if(cts.IsCancellationRequested)
                throw new XunitException("Failed to clean up database in 10 seconds");
        }
    }
}
