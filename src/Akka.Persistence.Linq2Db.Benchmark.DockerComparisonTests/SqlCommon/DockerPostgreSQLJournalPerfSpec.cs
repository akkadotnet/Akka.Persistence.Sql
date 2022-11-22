using Akka.Configuration;
using Akka.Persistence.Linq2Db.BenchmarkTests;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Benchmark.DockerComparisonTests.SqlCommon
{
    [Collection("PostgreSQLSpec")]
    public class DockerPostgreSQLJournalPerfSpec : L2dbJournalPerfSpec
    {
        public DockerPostgreSQLJournalPerfSpec(ITestOutputHelper output, PostgreSqlFixture fixture) : base(InitConfig(fixture),"sqlserverperfspec", output,40, TestConstants.DockerNumMessages)
        {
        }
        public static Config InitConfig(PostgreSqlFixture fixture)
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
            connection-string = ""{fixture.ConnectionString}""
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
    }
}