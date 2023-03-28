// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlSqlJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Containers;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.PostgreSql
{
    [Collection(nameof(PostgreSqlPersistenceBenchmark))]
    public class PostgreSqlSqlJournalPerfSpec : SqlJournalPerfSpec<PostgreSqlContainer>
    {
        public PostgreSqlSqlJournalPerfSpec(
            ITestOutputHelper output,
            PostgreSqlContainer fixture)
            : base(
                Configuration(fixture.ConnectionString),
                nameof(PostgreSqlSqlJournalPerfSpec),
                output,
                fixture,
                40,
                eventsCount: TestConstants.DockerNumMessages)
        {
        }

        private static Configuration.Config Configuration(string connString)
            => $@"
                akka.persistence {{
                    publish-plugin-commands = on
                    journal {{
                        plugin = ""akka.persistence.journal.sql""
                        sql {{
                            class = ""{typeof(SqlWriteJournal).AssemblyQualifiedName}""
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
                }}";

        [Fact]
        public void PersistenceActor_Must_measure_PersistGroup1000()
            => RunGroupBenchmark(1000, 10);
    }
}
