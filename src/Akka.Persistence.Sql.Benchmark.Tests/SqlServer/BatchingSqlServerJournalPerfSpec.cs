// -----------------------------------------------------------------------
//  <copyright file="BatchingSqlServerJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.SqlServer
{
    [Collection("BenchmarkSpec")]
    public class BatchingSqlServerJournalPerfSpec : SqlJournalPerfSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public BatchingSqlServerJournalPerfSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                Configure(fixture),
                nameof(BatchingSqlServerJournalPerfSpec),
                output,
                40,
                TestConstants.DockerNumMessages)
            => _fixture = fixture;

        public async Task InitializeAsync()
            => await _fixture.InitializeDbAsync(Database.SqlServer).ConfigureAwait(false);

        public Task DisposeAsync()
            => Task.CompletedTask;

        public static Configuration.Config Configure(TestFixture fixture)
            => $@"
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

        [Fact]
        public void PersistenceActor_Must_measure_PersistGroup1000()
            => RunGroupBenchmark(1000, 10);
    }
}
