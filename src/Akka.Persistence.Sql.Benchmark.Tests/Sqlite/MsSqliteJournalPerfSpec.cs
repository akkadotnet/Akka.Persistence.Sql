// -----------------------------------------------------------------------
//  <copyright file="SqliteJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.Sqlite
{
    [Collection(nameof(MsSqlitePersistenceBenchmark))]
    public class MsSqliteJournalPerfSpec : SqlJournalPerfSpec<MsSqliteContainer>
    {
        public MsSqliteJournalPerfSpec(
            ITestOutputHelper output,
            MsSqliteContainer fixture)
            : base(
                CreateSpecConfig(fixture.ConnectionString),
                nameof(MsSqliteJournalPerfSpec),
                output,
                fixture,
                eventsCount: TestConstants.NumMessages)
        {
        }

        private static Configuration.Config CreateSpecConfig(string connectionString)
            => @$"
                akka.persistence {{
                    publish-plugin-commands = on
                    journal {{
                        plugin = ""akka.persistence.journal.sqlite""
                        sqlite {{
                            class = ""Akka.Persistence.Sqlite.Journal.SqliteJournal, Akka.Persistence.Sqlite""
                            #plugin-dispatcher = ""akka.actor.default-dispatcher""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            table-name = event_journal
                            metadata-table-name = journal_metadata
                            auto-initialize = on
                            connection-string = ""{connectionString}""
                        }}
                    }}
                }}";
    }
}
