using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Linq2Db.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Benchmark.Tests.PostgreSql
{
    [Collection("BenchmarkSpec")]
    public class PostgreSqlJournalPerfSpec : L2dbJournalPerfSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public PostgreSqlJournalPerfSpec(ITestOutputHelper output, TestFixture fixture)
            : base(InitConfig(fixture), nameof(PostgreSqlJournalPerfSpec), output, 40, TestConstants.DockerNumMessages)
        {
            _fixture = fixture;
        }

        public static Config InitConfig(TestFixture fixture)
        {
            //need to make sure db is created before the tests start
            var specString = $@"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.postgresql""
        postgresql {{
            class = ""Akka.Persistence.PostgreSql.Journal.PostgreSqlJournal, Akka.Persistence.PostgreSql""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            table-name = EventJournal
            metadata-table-name = metadata
            schema-name = public
            auto-initialize = on
            connection-string = ""{fixture.ConnectionString(Database.PostgreSql)}""
        }}
    }}
}}";

            return ConfigurationFactory.ParseString(specString);
        }

        [Fact]
        public void PersistenceActor_Must_measure_PersistGroup1000()
        {
            RunGroupBenchmark(1000,10);
        }

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.PostgreSql);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}