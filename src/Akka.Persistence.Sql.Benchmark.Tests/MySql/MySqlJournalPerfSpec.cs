// -----------------------------------------------------------------------
//  <copyright file="SqlServerJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.MySql;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.SqlServer;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.MySql
{
    [Collection(nameof(MySqlPersistenceBenchmark))]
    public class MySqlJournalPerfSpec : SqlJournalPerfSpec<MySqlContainer>
    {
        public MySqlJournalPerfSpec(ITestOutputHelper output, MySqlContainer fixture)
            : base(
                Configuration(fixture),
                nameof(MySqlJournalPerfSpec),
                output,
                40,
                eventsCount: TestConstants.DockerNumMessages) { }

        private static Configuration.Config Configuration(MySqlContainer fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            return ConfigurationFactory.ParseString(
$$"""
akka.persistence {
    publish-plugin-commands = on
    journal {
        plugin = "akka.persistence.journal.mysql"
        mysql {
            auto-initialize = on
            connection-string = "{{fixture.ConnectionString}}"
        }
    }
}
""")
                .WithFallback(Persistence.DefaultConfig())
                .WithFallback(MySqlPersistence.DefaultConfiguration());
        }
    }
}
