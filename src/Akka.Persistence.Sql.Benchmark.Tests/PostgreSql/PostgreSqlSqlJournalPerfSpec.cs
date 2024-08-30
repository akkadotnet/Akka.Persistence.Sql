// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlSqlJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.PostgreSql
{
    [Collection(nameof(PostgreSqlPersistenceBenchmark))]
    public class PostgreSqlSqlCsvJournalPerfSpec : BasePostgreSqlSqlJournalPerfSpec
    {
        public PostgreSqlSqlCsvJournalPerfSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
        :base(TagMode.Csv, nameof(PostgreSqlSqlCsvJournalPerfSpec), output, fixture)
        {
        }
    }
    
    [Collection(nameof(PostgreSqlPersistenceBenchmark))]
    public class PostgreSqlSqlTagTableJournalPerfSpec : BasePostgreSqlSqlJournalPerfSpec
    {
        public PostgreSqlSqlTagTableJournalPerfSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            :base(TagMode.TagTable, nameof(PostgreSqlSqlTagTableJournalPerfSpec), output, fixture)
        {
        }
    }
    
    public abstract class BasePostgreSqlSqlJournalPerfSpec : SqlJournalPerfSpec<PostgreSqlContainer>
    {
        protected BasePostgreSqlSqlJournalPerfSpec(
            TagMode tagMode,
            string name,
            ITestOutputHelper output,
            PostgreSqlContainer fixture)
            : base(
                Configuration(fixture, tagMode),
                name,
                output,
                40,
                eventsCount: TestConstants.DockerNumMessages) { }

        private static Configuration.Config Configuration(PostgreSqlContainer fixture, TagMode tagMode)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            return ConfigurationFactory.ParseString(
                    $$"""
akka.persistence {
    publish-plugin-commands = on
    journal {
        plugin = "akka.persistence.journal.sql"
        sql {
            connection-string = "{{fixture.ConnectionString}}"
            provider-name = "{{fixture.ProviderName}}"
            tag-write-mode = {{tagMode}}
            use-clone-connection = true
            auto-initialize = true
        }
    }
}
""")
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }

        [Fact]
        public async Task PersistenceActor_Must_measure_PersistGroup1000()
            => await RunGroupBenchmarkAsync(1000, 10);
    }
}
