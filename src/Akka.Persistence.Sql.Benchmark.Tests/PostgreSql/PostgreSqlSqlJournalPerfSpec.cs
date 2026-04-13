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

    /// <summary>
    ///     CSV perf spec with forced event tagging (2 tags per event).
    /// </summary>
    [Collection(nameof(PostgreSqlPersistenceBenchmark))]
    public class PostgreSqlSqlCsvTaggedJournalPerfSpec : BasePostgreSqlSqlJournalPerfSpec
    {
        public PostgreSqlSqlCsvTaggedJournalPerfSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(
                TagMode.Csv,
                nameof(PostgreSqlSqlCsvTaggedJournalPerfSpec),
                output,
                fixture,
                forceTagging: true)
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
        /// <summary>
        ///     Base constructor for PostgreSQL journal perf specs~ uwu 🐘✨
        ///     <para>
        ///         <paramref name="payloadSizeBytes"/> lets you attach a random <c>byte[]</c>
        ///         blob to every persisted <see cref="Cmd"/> so we can measure realistic I/O.
        ///     </para>
        /// </summary>
        protected BasePostgreSqlSqlJournalPerfSpec(
            TagMode tagMode,
            string name,
            ITestOutputHelper output,
            PostgreSqlContainer fixture,
            bool useAsQueryableLiteralInsert = false,
            bool forceTagging = false,
            int payloadSizeBytes = 0)
            : base(
                Configuration(fixture, tagMode, useAsQueryableLiteralInsert, forceTagging),
                name,
                output,
                40,
                eventsCount: TestConstants.DockerNumMessages,
                payloadSizeBytes: payloadSizeBytes) { }

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

    /// <summary>
    ///     CSV perf spec with a 1 KB <c>byte[]</c> payload on every event.
    /// </summary>
    [Collection(nameof(PostgreSqlPersistenceBenchmark))]
    public class PostgreSqlSqlCsvLargePayloadJournalPerfSpec : BasePostgreSqlSqlJournalPerfSpec
    {
        public PostgreSqlSqlCsvLargePayloadJournalPerfSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(
                TagMode.Csv,
                nameof(PostgreSqlSqlCsvLargePayloadJournalPerfSpec),
                output,
                fixture,
                payloadSizeBytes: TestConstants.LargePayloadSizeBytes)
        {
        }
    }

    /// <summary>
    ///     TagTable perf spec with a 1 KB <c>byte[]</c> payload on every event.
    /// </summary>
    [Collection(nameof(PostgreSqlPersistenceBenchmark))]
    public class PostgreSqlSqlTagTableLargePayloadJournalPerfSpec : BasePostgreSqlSqlJournalPerfSpec
    {
        public PostgreSqlSqlTagTableLargePayloadJournalPerfSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(PostgreSqlSqlTagTableLargePayloadJournalPerfSpec),
                output,
                fixture,
                payloadSizeBytes: TestConstants.LargePayloadSizeBytes)
        {
        }
    }

    /// <summary>
    ///     TagTable perf spec with a 1 KB <c>byte[]</c> payload AND forced tagging (2 tags per event).
    /// </summary>
    [Collection(nameof(PostgreSqlPersistenceBenchmark))]
    public class PostgreSqlSqlTagTableLargePayloadTaggedJournalPerfSpec : BasePostgreSqlSqlJournalPerfSpec
    {
        public PostgreSqlSqlTagTableLargePayloadTaggedJournalPerfSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(PostgreSqlSqlTagTableLargePayloadTaggedJournalPerfSpec),
                output,
                fixture,
                forceTagging: true,
                payloadSizeBytes: TestConstants.LargePayloadSizeBytes)
        {
        }
    }
    
    // <summary>
    ///     TagTable perf spec with a 1 KB <c>byte[]</c> payload AND forced tagging (2 tags per event).
    /// </summary>
    [Collection(nameof(PostgreSqlPersistenceBenchmark))]
    public class PostgreSqlSqlTagTableAsQueryableLargePayloadTaggedJournalPerfSpec : BasePostgreSqlSqlJournalPerfSpec
    {
        public PostgreSqlSqlTagTableAsQueryableLargePayloadTaggedJournalPerfSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(PostgreSqlSqlTagTableLargePayloadTaggedJournalPerfSpec),
                output,
                fixture,
                forceTagging: true,
                payloadSizeBytes: TestConstants.LargePayloadSizeBytes,
                useAsQueryableLiteralInsert: true)
        {
        }
    }
}
