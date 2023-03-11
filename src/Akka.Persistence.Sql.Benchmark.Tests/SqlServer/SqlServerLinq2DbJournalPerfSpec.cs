using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Journal;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.SqlServer
{

    [Collection("BenchmarkSpec")]
    public class SqlServerLinq2DbJournalPerfSpec : L2dbJournalPerfSpec, IAsyncLifetime
    {
        private static Configuration.Config Configure(string connString)
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
            provider-name = ""{ProviderName.SqlServer2017}""
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

        public SqlServerLinq2DbJournalPerfSpec(ITestOutputHelper output, TestFixture fixture)
            : base(
                Configure(fixture.ConnectionString(Database.SqlServer)),
                nameof(SqlServerLinq2DbJournalPerfSpec), output, 40, eventsCount: TestConstants.DockerNumMessages)
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
            await _fixture.InitializeDbAsync(Database.SqlServer);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
