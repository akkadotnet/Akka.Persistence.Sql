// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.PostgreSql;
using Akka.Persistence.Sql.Tests.Common.Containers;
using FluentAssertions.Extensions;
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
                40,
                TestConstants.DockerNumMessages) { }

        public static Configuration.Config InitConfig(PostgreSqlContainer fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            return ConfigurationFactory.ParseString(
                    @$"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.postgresql""
        postgresql {{
            auto-initialize = on
            connection-string = ""{fixture.ConnectionString}""
        }}
    }}
}}")
                .WithFallback(PostgreSqlPersistence.DefaultConfiguration());
        }

        [Fact]
        public void PersistenceActor_Must_measure_PersistGroup1000()
            => RunGroupBenchmark(1000, 10);
    }
}
