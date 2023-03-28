// -----------------------------------------------------------------------
//  <copyright file="SqlServerSqlJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Tests.Common.Containers;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.SqlServer
{
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerLinq2DbJournalPerfSpec : SqlJournalPerfSpec<SqlServerContainer>
    {
        public SqlServerLinq2DbJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(
                Configure(fixture.ConnectionString),
                nameof(SqlServerLinq2DbJournalPerfSpec),
                output,
                fixture,
                40,
                eventsCount: TestConstants.DockerNumMessages)
        {
        }

        private static Configuration.Config Configure(string connString)
            => $@"
                akka.persistence {{
                    publish-plugin-commands = on
                    journal {{
                        plugin = ""akka.persistence.journal.sql""
                        sql {{
                            class = ""{typeof(SqlWriteJournal).AssemblyQualifiedName}""
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
                }}";

        [Fact]
        public void PersistenceActor_Must_measure_PersistGroup1000()
            => RunGroupBenchmark(1000, 10);
    }
}
