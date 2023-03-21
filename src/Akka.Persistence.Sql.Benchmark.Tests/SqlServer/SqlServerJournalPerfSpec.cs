// -----------------------------------------------------------------------
//  <copyright file="SqlServerJournalPerfSpec.cs" company="Akka.NET Project">
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
    public class SqlServerJournalPerfSpec : SqlJournalPerfSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public SqlServerJournalPerfSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                Configuration(fixture),
                nameof(SqlServerJournalPerfSpec),
                output,
                40,
                eventsCount: TestConstants.DockerNumMessages)
            => _fixture = fixture;

        public async Task InitializeAsync()
            => await _fixture.InitializeDbAsync(Database.SqlServer);

        public Task DisposeAsync()
            => Task.CompletedTask;

        private static Configuration.Config Configuration(TestFixture fixture)
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
                            connection-string = ""{fixture.ConnectionString(Database.SqlServer)}""
                        }}
                    }}
                }}";
    }
}
