using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Linq2Db.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Benchmark.Tests.Postgres
{
    [Collection("BenchmarkSpec")]
    public class PostgreSQLJournalPerfSpec : L2dbJournalPerfSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;
            
        public PostgreSQLJournalPerfSpec(ITestOutputHelper output, TestFixture fixture) 
            : base(InitConfig(fixture), nameof(PostgreSQLJournalPerfSpec), output, 40, TestConstants.DockerNumMessages)
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
            connection-string = ""{fixture.ConnectionString(Database.Postgres)}""
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
            await _fixture.InitializeDbAsync(Database.Postgres);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}