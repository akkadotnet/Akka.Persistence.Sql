// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.PostgreSql
{
    [Collection("BenchmarkSpec")]
    public class PostgreSqlJournalPerfSpec : L2dbJournalPerfSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public PostgreSqlJournalPerfSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                InitConfig(fixture),
                nameof(PostgreSqlJournalPerfSpec),
                output,
                40,
                TestConstants.DockerNumMessages)
            => _fixture = fixture;

        public async Task InitializeAsync()
            => await _fixture.InitializeDbAsync(Database.PostgreSql);

        public Task DisposeAsync()
            => Task.CompletedTask;

        public static Configuration.Config InitConfig(TestFixture fixture)
            => $@"
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

        [Fact]
        public void PersistenceActor_Must_measure_PersistGroup1000()
            => RunGroupBenchmark(1000, 10);
    }
}
