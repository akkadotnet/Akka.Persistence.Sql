// -----------------------------------------------------------------------
//  <copyright file="BatchingSqlServerJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.SqlServer;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.SqlServer
{
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class BatchingSqlServerJournalPerfSpec : SqlJournalPerfSpec<SqlServerContainer>
    {
        public BatchingSqlServerJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(
                Configure(fixture),
                nameof(BatchingSqlServerJournalPerfSpec),
                output,
                40,
                TestConstants.DockerNumMessages) { }

        public static Configuration.Config Configure(SqlServerContainer fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            return ConfigurationFactory.ParseString(
                    @$"
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
                            connection-string = ""{fixture.ConnectionString}""
                        }}
                    }}
                }}")
                .WithFallback(SqlServerPersistence.DefaultConfiguration());
        }

        [Fact]
        public void PersistenceActor_Must_measure_PersistGroup1000()
            => RunGroupBenchmark(1000, 10);
    }
}
