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

        public MsSqliteLinq2DbJournalPerfSpec(ITestOutputHelper output, TestFixture fixture)
            : base(
                SqliteJournalSpecConfig.Create(fixture.ConnectionString(Database.MsSqlite), ProviderName.SQLiteMS),
                nameof(MsSqliteLinq2DbJournalPerfSpec), output, eventsCount: TestConstants.NumMessages)
        {
            _fixture = fixture;
        }

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.MsSqlite);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
