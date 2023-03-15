// -----------------------------------------------------------------------
//  <copyright file="MsSqliteLinq2DbJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Sqlite;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.Sqlite
{
    [Collection("BenchmarkSpec")]
    public class MsSqliteLinq2DbJournalPerfSpec : L2dbJournalPerfSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public MsSqliteLinq2DbJournalPerfSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                SqliteJournalSpecConfig.Create(fixture.ConnectionString(Database.MsSqlite), ProviderName.SQLiteMS),
                nameof(MsSqliteLinq2DbJournalPerfSpec),
                output,
                eventsCount: TestConstants.NumMessages)
            => _fixture = fixture;

        public async Task InitializeAsync()
            => await _fixture.InitializeDbAsync(Database.MsSqlite);

        public Task DisposeAsync()
            => Task.CompletedTask;
    }
}
