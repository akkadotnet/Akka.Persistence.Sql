// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlLinq2DbJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Tests.Common;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.PostgreSql
{
    [Collection("BenchmarkSpec")]
    public class PostgreSqlLinq2DbJournalPerfSpec : L2dbJournalPerfSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public PostgreSqlLinq2DbJournalPerfSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                Configuration(fixture.ConnectionString(Database.PostgreSql)),
                nameof(PostgreSqlLinq2DbJournalPerfSpec),
                output,
                40,
                eventsCount: TestConstants.DockerNumMessages)
            => _fixture = fixture;

        public async Task InitializeAsync()
            => await _fixture.InitializeDbAsync(Database.PostgreSql);

        public Task DisposeAsync()
            => Task.CompletedTask;

        private static Configuration.Config Configuration(string connString)
            => ConfigurationFactory.ParseString(
                $@"
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

        [Fact]
        public void PersistenceActor_Must_measure_PersistGroup1000()
            => RunGroupBenchmark(1000, 10);
    }
}
