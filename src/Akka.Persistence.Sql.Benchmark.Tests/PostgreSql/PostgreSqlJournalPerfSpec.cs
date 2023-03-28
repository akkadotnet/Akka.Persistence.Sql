// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.PostgreSql
{
    [Collection(nameof(PostgreSqlPersistenceBenchmark))]
    public class PostgreSqlJournalPerfSpec : SqlJournalPerfSpec<PostgreSqlContainer>
    {
        public PostgreSqlJournalPerfSpec(
            ITestOutputHelper output,
            PostgreSqlContainer fixture)
            : base(
                InitConfig(fixture),
                nameof(PostgreSqlJournalPerfSpec),
                output,
                fixture,
                40,
                TestConstants.DockerNumMessages)
        {
        }

        public static Configuration.Config InitConfig(PostgreSqlContainer fixture)
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
                            connection-string = ""{fixture.ConnectionString}""
                        }}
                    }}
                }}";

        [Fact]
        public void PersistenceActor_Must_measure_PersistGroup1000()
            => RunGroupBenchmark(1000, 10);
    }
}
