using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Linq2Db.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Benchmark.Tests.SqlServer
{
    [Collection("BenchmarkSpec")]
    public class SqlServerJournalPerfSpec : L2dbJournalPerfSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;
        
        public SqlServerJournalPerfSpec(ITestOutputHelper output, TestFixture fixture) 
            : base( 
                Configuration(fixture), nameof(SqlServerJournalPerfSpec), 
                output, 40, eventsCount: TestConstants.DockerNumMessages)
        {
            _fixture = fixture;
        }
        
        private static Config Configuration(TestFixture fixture)
        {
            var specString = $@"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.sql-server""
        sql-server {{
            class = ""Akka.Persistence.SqlServer.Journal.SqlServerJournal, Akka.Persistence.SqlServer""
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