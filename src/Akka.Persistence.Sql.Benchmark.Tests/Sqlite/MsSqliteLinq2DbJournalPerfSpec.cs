// -----------------------------------------------------------------------
//  <copyright file="MsSqliteLinq2DbJournalPerfSpec.cs" company="Akka.NET Project">
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

namespace Akka.Persistence.Sql.Benchmark.Tests.Sqlite
{
    // -----------------------------------------------------------------------
    // 🐾 CSV variants
    // -----------------------------------------------------------------------

    /// <summary>CSV tag-mode perf spec - no forced tagging.</summary>
    [Collection(nameof(MsSqlitePersistenceBenchmark))]
    public class MsSqliteLinq2DbCsvJournalPerfSpec : BaseMsSqliteLinq2DbJournalPerfSpec
    {
        public MsSqliteLinq2DbCsvJournalPerfSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(TagMode.Csv, nameof(MsSqliteLinq2DbCsvJournalPerfSpec), output, fixture) { }
    }

    /// <summary>
    ///     CSV perf spec with forced event tagging (2 tags per event). UwU~
    /// </summary>
    [Collection(nameof(MsSqlitePersistenceBenchmark))]
    public class MsSqliteLinq2DbCsvTaggedJournalPerfSpec : BaseMsSqliteLinq2DbJournalPerfSpec
    {
        public MsSqliteLinq2DbCsvTaggedJournalPerfSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(
                TagMode.Csv,
                nameof(MsSqliteLinq2DbCsvTaggedJournalPerfSpec),
                output,
                fixture,
                forceTagging: true) { }
    }

    // -----------------------------------------------------------------------
    // 🐾 TagTable variants
    // -----------------------------------------------------------------------

    /// <summary>TagTable perf spec - no extra options.</summary>
    [Collection(nameof(MsSqlitePersistenceBenchmark))]
    public class MsSqliteLinq2DbTagTableJournalPerfSpec : BaseMsSqliteLinq2DbJournalPerfSpec
    {
        public MsSqliteLinq2DbTagTableJournalPerfSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(TagMode.TagTable, nameof(MsSqliteLinq2DbTagTableJournalPerfSpec), output, fixture) { }
    }

    /// <summary>
    ///     TagTable perf spec with <c>use-tagtable-asqueryable-literal-insert</c> enabled.
    ///     Uses <c>AsQueryable()</c> + <c>InsertWithOutputAsync()</c> for tag inserts~ ✨
    /// </summary>
    [Collection(nameof(MsSqlitePersistenceBenchmark))]
    public class MsSqliteLinq2DbTagTableAsQueryableJournalPerfSpec : BaseMsSqliteLinq2DbJournalPerfSpec
    {
        public MsSqliteLinq2DbTagTableAsQueryableJournalPerfSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(MsSqliteLinq2DbTagTableAsQueryableJournalPerfSpec),
                output,
                fixture,
                useAsQueryableLiteralInsert: true) { }
    }

    /// <summary>
    ///     TagTable perf spec with forced event tagging (2 tags per event).
    ///     <c>AsQueryable</c> optimization is disabled.
    /// </summary>
    [Collection(nameof(MsSqlitePersistenceBenchmark))]
    public class MsSqliteLinq2DbTagTableTaggedJournalPerfSpec : BaseMsSqliteLinq2DbJournalPerfSpec
    {
        public MsSqliteLinq2DbTagTableTaggedJournalPerfSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(MsSqliteLinq2DbTagTableTaggedJournalPerfSpec),
                output,
                fixture,
                forceTagging: true) { }
    }

    /// <summary>
    ///     TagTable perf spec with forced event tagging (2 tags per event) AND
    ///     <c>use-tagtable-asqueryable-literal-insert</c> enabled. Double combo~ UwU
    /// </summary>
    [Collection(nameof(MsSqlitePersistenceBenchmark))]
    public class MsSqliteLinq2DbTagTableAsQueryableTaggedJournalPerfSpec : BaseMsSqliteLinq2DbJournalPerfSpec
    {
        public MsSqliteLinq2DbTagTableAsQueryableTaggedJournalPerfSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(MsSqliteLinq2DbTagTableAsQueryableTaggedJournalPerfSpec),
                output,
                fixture,
                useAsQueryableLiteralInsert: true,
                forceTagging: true) { }
    }

    // -----------------------------------------------------------------------
    // 🐾 Large-payload variants (1 KB byte[] blob per event)
    // -----------------------------------------------------------------------

    /// <summary>CSV perf spec with a 1 KB <c>byte[]</c> payload on every event.</summary>
    [Collection(nameof(MsSqlitePersistenceBenchmark))]
    public class MsSqliteLinq2DbCsvLargePayloadJournalPerfSpec : BaseMsSqliteLinq2DbJournalPerfSpec
    {
        public MsSqliteLinq2DbCsvLargePayloadJournalPerfSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(
                TagMode.Csv,
                nameof(MsSqliteLinq2DbCsvLargePayloadJournalPerfSpec),
                output,
                fixture,
                payloadSizeBytes: TestConstants.LargePayloadSizeBytes) { }
    }

    /// <summary>TagTable perf spec with a 1 KB <c>byte[]</c> payload on every event.</summary>
    [Collection(nameof(MsSqlitePersistenceBenchmark))]
    public class MsSqliteLinq2DbTagTableLargePayloadJournalPerfSpec : BaseMsSqliteLinq2DbJournalPerfSpec
    {
        public MsSqliteLinq2DbTagTableLargePayloadJournalPerfSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(MsSqliteLinq2DbTagTableLargePayloadJournalPerfSpec),
                output,
                fixture,
                payloadSizeBytes: TestConstants.LargePayloadSizeBytes) { }
    }

    /// <summary>
    ///     TagTable perf spec with a 1 KB <c>byte[]</c> payload AND forced tagging (2 tags per event). 🐾
    /// </summary>
    [Collection(nameof(MsSqlitePersistenceBenchmark))]
    public class MsSqliteLinq2DbTagTableLargePayloadTaggedJournalPerfSpec : BaseMsSqliteLinq2DbJournalPerfSpec
    {
        public MsSqliteLinq2DbTagTableLargePayloadTaggedJournalPerfSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(MsSqliteLinq2DbTagTableLargePayloadTaggedJournalPerfSpec),
                output,
                fixture,
                forceTagging: true,
                payloadSizeBytes: TestConstants.LargePayloadSizeBytes) { }
    }

    /// <summary>
    ///     TagTable perf spec with a 1 KB <c>byte[]</c> payload, forced tagging (2 tags per event),
    ///     AND <c>use-tagtable-asqueryable-literal-insert</c> enabled. Maximum combo~ ✨UwU✨
    /// </summary>
    [Collection(nameof(MsSqlitePersistenceBenchmark))]
    public class MsSqliteLinq2DbTagTableAsQueryableLargePayloadTaggedJournalPerfSpec : BaseMsSqliteLinq2DbJournalPerfSpec
    {
        public MsSqliteLinq2DbTagTableAsQueryableLargePayloadTaggedJournalPerfSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(
                TagMode.TagTable,
                nameof(MsSqliteLinq2DbTagTableAsQueryableLargePayloadTaggedJournalPerfSpec),
                output,
                fixture,
                useAsQueryableLiteralInsert: true,
                forceTagging: true,
                payloadSizeBytes: TestConstants.LargePayloadSizeBytes) { }
    }

    // -----------------------------------------------------------------------
    // 🐾 Base class - mirrors BasePostgreSqlSqlJournalPerfSpec
    // -----------------------------------------------------------------------

    /// <summary>
    ///     Base class for all MsSqlite Linq2Db journal perf specs.
    ///     Builds the HOCON config dynamically based on the chosen tag mode,
    ///     <c>useAsQueryableLiteralInsert</c>, <c>forceTagging</c>,
    ///     and an optional payload blob size. UwU~
    /// </summary>
    public abstract class BaseMsSqliteLinq2DbJournalPerfSpec : SqlJournalPerfSpec<MsSqliteContainer>
    {
        /// <param name="tagMode">CSV or TagTable.</param>
        /// <param name="name">Actor system / test name.</param>
        /// <param name="output">xUnit output helper.</param>
        /// <param name="fixture">The in-memory SQLite container fixture.</param>
        /// <param name="useAsQueryableLiteralInsert">
        ///     Enables <c>use-tagtable-asqueryable-literal-insert</c> for AsQueryable tag inserts.
        /// </param>
        /// <param name="forceTagging">Attach 2 tags to every event via <see cref="CmdEventTagger"/>.</param>
        /// <param name="payloadSizeBytes">
        ///     When &gt; 0, a random <c>byte[]</c> blob of this size is attached to every event
        ///     so we can measure realistic I/O. 🐾
        /// </param>
        protected BaseMsSqliteLinq2DbJournalPerfSpec(
            TagMode tagMode,
            string name,
            ITestOutputHelper output,
            MsSqliteContainer fixture,
            bool useAsQueryableLiteralInsert = false,
            bool forceTagging = false,
            int payloadSizeBytes = 0)
            : base(
                Configuration(fixture, tagMode, useAsQueryableLiteralInsert, forceTagging),
                name,
                output,
                eventsCount: TestConstants.NumMessages,
                payloadSizeBytes: payloadSizeBytes) { }

        /// <summary>
        ///     Builds HOCON config for the SQLite journal with the given options.
        ///     <para>
        ///         CopilotNote: each spec variant gets a fresh in-memory DB via
        ///         <see cref="MsSqliteContainer.InitializeDbAsync"/> to avoid state bleed. 🐾
        ///     </para>
        /// </summary>
        private static Configuration.Config Configuration(
            MsSqliteContainer fixture,
            TagMode tagMode,
            bool useAsQueryableLiteralInsert,
            bool forceTagging)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to initialize SQLite in-memory database in 10 seconds");

            // 🐾 Optionally wire up the CmdEventTagger to stamp 2 tags on every persisted event.
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

        /// <summary>Group benchmark with 1000 actors × 10 messages each. ✨</summary>
        [Fact]
        public async Task PersistenceActor_Must_measure_PersistGroup1000()
            => await RunGroupBenchmarkAsync(1000, 10);
    }
}
