// -----------------------------------------------------------------------
//  <copyright file="SqlServerLinq2DbJournalPerfSpec.cs" company="Akka.NET Project">
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

namespace Akka.Persistence.Sql.Benchmark.Tests.SqlServer
{
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerLinq2DbCsvJournalPerfSpec : BaseSqlServerLinq2DbJournalPerfSpec
    {
        public SqlServerLinq2DbCsvJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(TagMode.Csv, nameof(SqlServerLinq2DbCsvJournalPerfSpec), output, fixture)
        {
        }
    }

    /// <summary>
    ///     CSV perf spec with forced event tagging (2 tags per event).
    /// </summary>
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerLinq2DbCsvTaggedJournalPerfSpec : BaseSqlServerLinq2DbJournalPerfSpec
    {
        public SqlServerLinq2DbCsvTaggedJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(
                TagMode.Csv,
                nameof(SqlServerLinq2DbCsvTaggedJournalPerfSpec),
                output,
                fixture,
                forceTagging: true)
        {
        }
    }

    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerLinq2DbTagTableJournalPerfSpec : BaseSqlServerLinq2DbJournalPerfSpec
    {
        public SqlServerLinq2DbTagTableJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(TagMode.TagTable, nameof(SqlServerLinq2DbTagTableJournalPerfSpec), output, fixture)
        {
        }
    }

    /// <summary>
    ///     TagTable perf spec with <c>tagtable-asqueryable-insert-mode = inline</c> enabled.
    ///     Uses <c>AsQueryable()</c> + <c>InsertWithOutputAsync()</c> for tag inserts
    /// </summary>
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerLinq2DbTagTableAsQueryableJournalPerfSpec : BaseSqlServerLinq2DbJournalPerfSpec
    {
        public SqlServerLinq2DbTagTableAsQueryableJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(SqlServerLinq2DbTagTableAsQueryableJournalPerfSpec),
                output,
                fixture,
                tagTableQueryableInsertMode: TagTableQueryableInsertMode.Inline)
        {
        }
    }

    /// <summary>
    ///     TagTable perf spec with <c>tagtable-asqueryable-insert-mode = parameterized</c> enabled.
    ///     Uses <c>AsQueryable().Parameterize()</c> for tag inserts — all values as SQL parameters.
    /// </summary>
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerLinq2DbTagTableAsQueryableParameterizedJournalPerfSpec : BaseSqlServerLinq2DbJournalPerfSpec
    {
        public SqlServerLinq2DbTagTableAsQueryableParameterizedJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(SqlServerLinq2DbTagTableAsQueryableParameterizedJournalPerfSpec),
                output,
                fixture,
                tagTableQueryableInsertMode: TagTableQueryableInsertMode.Parameterized)
        {
        }
    }

    /// <summary>
    ///     TagTable perf spec with forced event tagging (2 tags per event).
    ///     AsQueryable optimization is disabled.
    /// </summary>
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerLinq2DbTagTableTaggedJournalPerfSpec : BaseSqlServerLinq2DbJournalPerfSpec
    {
        public SqlServerLinq2DbTagTableTaggedJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(SqlServerLinq2DbTagTableTaggedJournalPerfSpec),
                output,
                fixture,
                forceTagging: true)
        {
        }
    }

    /// <summary>
    ///     TagTable perf spec with forced event tagging (2 tags per event) AND
    ///     <c>tagtable-asqueryable-insert-mode = inline</c> is enabled.
    /// </summary>
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerLinq2DbTagTableAsQueryableTaggedJournalPerfSpec : BaseSqlServerLinq2DbJournalPerfSpec
    {
        public SqlServerLinq2DbTagTableAsQueryableTaggedJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(SqlServerLinq2DbTagTableAsQueryableTaggedJournalPerfSpec),
                output,
                fixture,
                tagTableQueryableInsertMode: TagTableQueryableInsertMode.Inline,
                forceTagging: true)
        {
        }
    }

    /// <summary>
    ///     TagTable perf spec with forced event tagging (2 tags per event) AND
    ///     <c>tagtable-asqueryable-insert-mode = parameterized</c> is enabled.
    /// </summary>
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerLinq2DbTagTableAsQueryableParameterizedTaggedJournalPerfSpec : BaseSqlServerLinq2DbJournalPerfSpec
    {
        public SqlServerLinq2DbTagTableAsQueryableParameterizedTaggedJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(SqlServerLinq2DbTagTableAsQueryableParameterizedTaggedJournalPerfSpec),
                output,
                fixture,
                tagTableQueryableInsertMode: TagTableQueryableInsertMode.Parameterized,
                forceTagging: true)
        {
        }
    }

    public abstract class BaseSqlServerLinq2DbJournalPerfSpec : SqlJournalPerfSpec<SqlServerContainer>
    {
        /// <summary>
        ///     Base constructor for SQL Server journal perf specs~
        ///     <para>
        ///         <paramref name="payloadSizeBytes"/> lets you attach a random <c>byte[]</c>
        ///         blob to every persisted <see cref="Cmd"/> so we can measure realistic I/O.
        ///     </para>
        /// </summary>
        protected BaseSqlServerLinq2DbJournalPerfSpec(
            TagMode tagMode,
            string name,
            ITestOutputHelper output,
            SqlServerContainer fixture,
            TagTableQueryableInsertMode tagTableQueryableInsertMode = TagTableQueryableInsertMode.Off,
            bool forceTagging = false,
            int payloadSizeBytes = 0)
            : base(
                Configure(fixture, tagMode, tagTableQueryableInsertMode, forceTagging),
                name,
                output,
                40,
                eventsCount: TestConstants.DockerNumMessages,
                payloadSizeBytes: payloadSizeBytes)
        {
        }

        private static Configuration.Config Configure(
            SqlServerContainer fixture,
            TagMode tagMode,
            TagTableQueryableInsertMode tagTableQueryableInsertMode = TagTableQueryableInsertMode.Off,
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
            tagtable-asqueryable-insert-mode = "{{tagTableQueryableInsertMode.ToString().ToLowerInvariant()}}"
            default {
                journal {
                    table-name = testPerfTable
                }
            }
        }
    }
}
""")
                .WithFallback(ConfigurationFactory.ParseString(taggingConfig))
                .WithFallback(Persistence.DefaultConfig())
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }

        [Fact]
        public async Task PersistenceActor_Must_measure_PersistGroup1000()
            => await RunGroupBenchmarkAsync(1000, 10);
    }

    // ── Large-payload specs ────────────────────────────────────────────
    // CopilotNotes: These specs attach a 1 KB random byte[] blob to every
    //               persisted Cmd so we can measure realistic serialisation
    //               + I/O overhead. 

    /// <summary>
    ///     CSV perf spec with a 1 KB <c>byte[]</c> payload on every event.
    /// </summary>
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerLinq2DbCsvLargePayloadJournalPerfSpec : BaseSqlServerLinq2DbJournalPerfSpec
    {
        public SqlServerLinq2DbCsvLargePayloadJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(
                TagMode.Csv,
                nameof(SqlServerLinq2DbCsvLargePayloadJournalPerfSpec),
                output,
                fixture,
                payloadSizeBytes: TestConstants.LargePayloadSizeBytes)
        {
        }
    }

    /// <summary>
    ///     TagTable perf spec with a 1 KB <c>byte[]</c> payload on every event.
    /// </summary>
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerLinq2DbTagTableLargePayloadJournalPerfSpec : BaseSqlServerLinq2DbJournalPerfSpec
    {
        public SqlServerLinq2DbTagTableLargePayloadJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(SqlServerLinq2DbTagTableLargePayloadJournalPerfSpec),
                output,
                fixture,
                payloadSizeBytes: TestConstants.LargePayloadSizeBytes)
        {
        }
    }

    /// <summary>
    ///     TagTable perf spec with a 1 KB <c>byte[]</c> payload AND forced tagging (2 tags per event).
    ///     Maximum realistic overhead scenario.
    /// </summary>
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerLinq2DbTagTableLargePayloadTaggedJournalPerfSpec : BaseSqlServerLinq2DbJournalPerfSpec
    {
        public SqlServerLinq2DbTagTableLargePayloadTaggedJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(SqlServerLinq2DbTagTableLargePayloadTaggedJournalPerfSpec),
                output,
                fixture,
                forceTagging: true,
                payloadSizeBytes: TestConstants.LargePayloadSizeBytes)
        {
        }
    }
}
