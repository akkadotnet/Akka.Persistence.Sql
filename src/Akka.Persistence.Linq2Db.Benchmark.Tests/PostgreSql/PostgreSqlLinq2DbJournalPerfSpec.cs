using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Linq2Db.Tests.Common;
using Akka.Persistence.Sql.Linq2Db.Journal;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Benchmark.Tests.PostgreSql
{
    [Collection("BenchmarkSpec")]
    public class PostgreSqlLinq2DbJournalPerfSpec : L2dbJournalPerfSpec, IAsyncLifetime
    {
        private static Config Configuration(string connString)
        {
            return ConfigurationFactory.ParseString($@"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.linq2db""
        linq2db {{
            class = ""{typeof(Linq2DbWriteJournal).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""

            connection-string = ""{connString}""
            provider-name = ""{ProviderName.PostgreSQL95}""
            use-clone-connection = true
            auto-initialize = true
            warn-on-auto-init-fail = false
            default {{
                journal {{
                    table-name = testPerfTable
                }}
            }}
        }}
    }}
}}");
        }

        private readonly TestFixture _fixture;

        public PostgreSqlLinq2DbJournalPerfSpec(ITestOutputHelper output,
            TestFixture fixture) : base(Configuration(fixture.ConnectionString(Database.PostgreSql)),
            nameof(PostgreSqlLinq2DbJournalPerfSpec), output, 40, eventsCount: TestConstants.DockerNumMessages)
        {
            _fixture = fixture;
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