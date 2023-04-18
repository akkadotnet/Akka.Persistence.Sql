// -----------------------------------------------------------------------
//  <copyright file="MsSqliteLinq2DbJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Sqlite;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests.Sqlite
{
    [Collection(nameof(MsSqlitePersistenceBenchmark))]
    public class MsSqliteLinq2DbJournalPerfSpec : SqlJournalPerfSpec<MsSqliteContainer>
    {
        public MsSqliteLinq2DbJournalPerfSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(
                SqliteJournalSpecConfig.Create(fixture),
                nameof(MsSqliteLinq2DbJournalPerfSpec),
                output,
                eventsCount: TestConstants.NumMessages) { }
    }
}
