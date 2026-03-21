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
using Xunit.Abstractions;

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
        
    [Collection(nameof(SqlServerPersistenceBenchmark))]
    public class SqlServerLinq2DbTagTableJournalPerfSpec : BaseSqlServerLinq2DbJournalPerfSpec
    {
        public SqlServerLinq2DbTagTableJournalPerfSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(TagMode.TagTable, nameof(SqlServerLinq2DbTagTableJournalPerfSpec), output, fixture)
        {
        }
    }

    /// <summary>
    ///     TagTable perf spec with <c>use-tagtable-asqueryable-literal-insert</c> enabled.
    ///     Uses <c>AsQueryable()</c> + <c>InsertWithOutputAsync()</c> for tag inserts
    ///     to measure the throughput improvement over standard tag table inserts. ✨
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
                useAsQueryableLiteralInsert: true)
        {
        }
    }

    public abstract class BaseSqlServerLinq2DbJournalPerfSpec : SqlJournalPerfSpec<SqlServerContainer>
    {
        protected BaseSqlServerLinq2DbJournalPerfSpec(
            TagMode tagMode,
            string name,
            ITestOutputHelper output,
            SqlServerContainer fixture,
            bool useAsQueryableLiteralInsert = false)
            : base(
                Configure(fixture, tagMode, useAsQueryableLiteralInsert),
                name,
                output,
                40,
                eventsCount: TestConstants.DockerNumMessages)
        {
        }

        private static Configuration.Config Configure(
            SqlServerContainer fixture,
            TagMode tagMode,
            bool useAsQueryableLiteralInsert = false)
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
            use-tagtable-asqueryable-literal-insert = {{useAsQueryableLiteralInsert.ToString().ToLowerInvariant()}}
            default {
                journal {
                    table-name = testPerfTable
                }
            }
        }
    }
}
""")
                .WithFallback(Persistence.DefaultConfig())
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }

        [Fact]
        public async Task PersistenceActor_Must_measure_PersistGroup1000()
            => await RunGroupBenchmarkAsync(1000, 10);
    }
}
