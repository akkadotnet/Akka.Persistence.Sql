// -----------------------------------------------------------------------
//  <copyright file="MsSqliteJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sqlite;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.Sqlite
{
    [Collection(nameof(MsSqlitePersistenceBenchmark))]
    public class MsSqliteJournalPerfSpec : SqlJournalPerfSpec<MsSqliteContainer>
    {
        public MsSqliteJournalPerfSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(
                CreateSpecConfig(fixture),
                nameof(MsSqliteJournalPerfSpec),
                output,
                eventsCount: TestConstants.NumMessages) { }

        private static Configuration.Config CreateSpecConfig(MsSqliteContainer fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            return ConfigurationFactory.ParseString(
                    @$"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.sqlite""
        sqlite {{
            auto-initialize = on
            connection-string = ""{fixture.ConnectionString}""
        }}
    }}
    snapshot-store.plugin = akka.persistence.snapshot-store.inmem
}}")
                .WithFallback(SqlitePersistence.DefaultConfiguration())
                .WithFallback(Persistence.DefaultConfig());
        }
    }
}
