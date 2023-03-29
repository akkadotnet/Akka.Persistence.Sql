// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlSqlJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Containers;
using FluentAssertions.Extensions;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.PostgreSql
{
    [Collection(nameof(PostgreSqlPersistenceBenchmark))]
    public class PostgreSqlSqlJournalPerfSpec : SqlJournalPerfSpec<PostgreSqlContainer>
    {
        public PostgreSqlSqlJournalPerfSpec(
            ITestOutputHelper output,
            PostgreSqlContainer fixture)
            : base(
                Configuration(fixture),
                nameof(PostgreSqlSqlJournalPerfSpec),
                output,
                40,
                eventsCount: TestConstants.DockerNumMessages)
        {
        }

        private static Configuration.Config Configuration(PostgreSqlContainer fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
            
            return ConfigurationFactory.ParseString(@$"
                akka.persistence {{
                    publish-plugin-commands = on
                    journal {{
                        plugin = ""akka.persistence.journal.sql""
                        sql {{
                            connection-string = ""{fixture.ConnectionString}""
                            provider-name = ""{fixture.ProviderName}""
                            use-clone-connection = true
                            auto-initialize = true
                            default {{
                                journal {{
                                    table-name = testPerfTable
                                }}
                            }}
                        }}
                    }}
                }}")
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }

        [Fact]
        public void PersistenceActor_Must_measure_PersistGroup1000()
            => RunGroupBenchmark(1000, 10);
    }
}
