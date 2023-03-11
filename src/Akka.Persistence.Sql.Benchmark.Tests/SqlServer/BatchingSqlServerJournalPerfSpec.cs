using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Linq2Db.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Benchmark.Tests.SqlServer
{
    [Collection("BenchmarkSpec")]
    public class BatchingSqlServerJournalPerfSpec : L2dbJournalPerfSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;
        
        public BatchingSqlServerJournalPerfSpec(ITestOutputHelper output, TestFixture fixture) 
            : base(
                Configure(fixture), nameof(BatchingSqlServerJournalPerfSpec), 
                output, 40, TestConstants.DockerNumMessages)
        {
            _fixture = fixture;
        }
        
        public static Config Configure(TestFixture fixture)
        {
            var specString = $@"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.sql-server""
        sql-server {{
            class = ""Akka.Persistence.SqlServer.Journal.BatchingSqlServerJournal, Akka.Persistence.SqlServer""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            table-name = EventJournal
            schema-name = dbo
            auto-initialize = on
            connection-string = ""{fixture.ConnectionString(Database.SqlServer)}""
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
            await _fixture.InitializeDbAsync(Database.SqlServer);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}