// -----------------------------------------------------------------------
//  <copyright file="MsSqliteSqlJournalPerfSpec.cs" company="Akka.NET Project">
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
    public class MsSqliteSqlJournalPerfSpec : SqlJournalPerfSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public MsSqliteSqlJournalPerfSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                SqliteJournalSpecConfig.Create(fixture.ConnectionString(Database.MsSqlite), ProviderName.SQLiteMS),
                nameof(MsSqliteSqlJournalPerfSpec),
                output,
                eventsCount: TestConstants.NumMessages)
            => _fixture = fixture;

        public async Task InitializeAsync()
            => await _fixture.InitializeDbAsync(Database.MsSqlite);

        public Task DisposeAsync()
            => Task.CompletedTask;
    }
}
