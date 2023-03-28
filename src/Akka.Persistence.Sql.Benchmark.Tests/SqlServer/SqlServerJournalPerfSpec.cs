// -----------------------------------------------------------------------
//  <copyright file="SqlServerJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.SqlServer
{
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerJournalPerfSpec : SqlJournalPerfSpec<SqlServerContainer>
    {
        public SqlServerJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(
                Configuration(fixture),
                nameof(SqlServerJournalPerfSpec),
                output,
                fixture,
                40,
                eventsCount: TestConstants.DockerNumMessages)
        {
        }

        private static Configuration.Config Configuration(SqlServerContainer fixture)
            => $@"
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
                            connection-string = ""{fixture.ConnectionString}""
                        }}
                    }}
                }}";
    }
}
