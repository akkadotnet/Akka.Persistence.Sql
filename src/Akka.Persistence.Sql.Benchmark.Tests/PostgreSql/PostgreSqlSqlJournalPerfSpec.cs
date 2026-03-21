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
    
    /// <summary>
    ///     TagTable perf spec with <c>use-tagtable-asqueryable-literal-insert</c> enabled.
    ///     Uses <c>AsQueryable()</c> + <c>InsertWithOutputAsync()</c> for tag inserts
    /// </summary>
    [Collection(nameof(PostgreSqlPersistenceBenchmark))]
    public class PostgreSqlSqlTagTableAsQueryableJournalPerfSpec : BasePostgreSqlSqlJournalPerfSpec
    {
        public PostgreSqlSqlTagTableAsQueryableJournalPerfSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(PostgreSqlSqlTagTableAsQueryableJournalPerfSpec),
                output,
                fixture,
                useAsQueryableLiteralInsert: true)
        {
        }
    }

    /// <summary>
    ///     TagTable perf spec with forced event tagging (2 tags per event).
    ///     AsQueryable optimization is disabled.
    /// </summary>
    [Collection(nameof(PostgreSqlPersistenceBenchmark))]
    public class PostgreSqlSqlTagTableTaggedJournalPerfSpec : BasePostgreSqlSqlJournalPerfSpec
    {
        public PostgreSqlSqlTagTableTaggedJournalPerfSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(PostgreSqlSqlTagTableTaggedJournalPerfSpec),
                output,
                fixture,
                forceTagging: true)
        {
        }
    }

    /// <summary>
    ///     TagTable perf spec with forced event tagging (2 tags per event) AND
    ///     <c>use-tagtable-asqueryable-literal-insert</c> enabled.
    /// </summary>
    [Collection(nameof(PostgreSqlPersistenceBenchmark))]
    public class PostgreSqlSqlTagTableAsQueryableTaggedJournalPerfSpec : BasePostgreSqlSqlJournalPerfSpec
    {
        public PostgreSqlSqlTagTableAsQueryableTaggedJournalPerfSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(PostgreSqlSqlTagTableAsQueryableTaggedJournalPerfSpec),
                output,
                fixture,
                useAsQueryableLiteralInsert: true,
                forceTagging: true)
        {
        }
    }

    public abstract class BasePostgreSqlSqlJournalPerfSpec : SqlJournalPerfSpec<PostgreSqlContainer>
    {
        protected BasePostgreSqlSqlJournalPerfSpec(
            TagMode tagMode,
            string name,
            ITestOutputHelper output,
            PostgreSqlContainer fixture,
            bool useAsQueryableLiteralInsert = false,
            bool forceTagging = false)
            : base(
                Configuration(fixture, tagMode, useAsQueryableLiteralInsert, forceTagging),
                name,
                output,
                40,
                eventsCount: TestConstants.DockerNumMessages) { }

        private static Configuration.Config Configuration(
            PostgreSqlContainer fixture,
            TagMode tagMode,
            bool useAsQueryableLiteralInsert = false,
            bool forceTagging = false)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            var taggingConfig = forceTagging
                ? """
                  akka.persistence.journal.sql {
                      event-adapter-bindings {
                          "Akka.Persistence.Sql.Benchmark.Tests.Cmd, Akka.Persistence.Sql.Benchmark.Tests" = perf-tagger
                      }
                      event-adapters {
                          perf-tagger = "Akka.Persistence.Sql.Benchmark.Tests.CmdEventTagger, Akka.Persistence.Sql.Benchmark.Tests"
                      }
                  }
                  """
                : "";

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
            use-tagtable-asqueryable-literal-insert = {{useAsQueryableLiteralInsert.ToString().ToLowerInvariant()}}
        }
    }
}
""")
                .WithFallback(ConfigurationFactory.ParseString(taggingConfig))
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }

        [Fact]
        public async Task PersistenceActor_Must_measure_PersistGroup1000()
            => await RunGroupBenchmarkAsync(1000, 10);
    }
}
