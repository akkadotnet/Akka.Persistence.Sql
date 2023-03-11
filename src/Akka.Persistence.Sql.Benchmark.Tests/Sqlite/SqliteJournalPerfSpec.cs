using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.Sqlite
{
    [Collection("BenchmarkSpec")]
    public class SqliteJournalPerfSpec : L2dbJournalPerfSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public SqliteJournalPerfSpec(ITestOutputHelper output, TestFixture fixture)
            : base(
                CreateSpecConfig(fixture.ConnectionString(Database.MsSqlite)),
                nameof(SqliteJournalPerfSpec), output, eventsCount: TestConstants.NumMessages)
        {
            _fixture = fixture;
        }

        private static Configuration.Config CreateSpecConfig(string connectionString)
        {
            return ConfigurationFactory.ParseString(@$"
                akka.persistence {{
                    publish-plugin-commands = on
                    journal {{
                        plugin = ""akka.persistence.journal.sqlite""
                        sqlite {{
                            class = ""Akka.Persistence.Sqlite.Journal.SqliteJournal, Akka.Persistence.Sqlite""
                            #plugin-dispatcher = ""akka.actor.default-dispatcher""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            table-name = event_journal
                            metadata-table-name = journal_metadata
                            auto-initialize = on
                            connection-string = ""{connectionString}""
                        }}
                    }}
                }}");
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
