// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlCommonJournalCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.PostgreSql.Compatibility
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(PostgreSqlPersistenceSpec))]
    public class PostgreSqlCommonJournalCompatibilitySpec : SqlCommonJournalCompatibilitySpec<PostgreSqlContainer>
    {
        public PostgreSqlCommonJournalCompatibilitySpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(fixture, output) { }

        protected override Func<PostgreSqlContainer, Configuration.Config> Config => fixture
            => PostgreSqlCompatibilitySpecConfig.InitJournalConfig(fixture, "event_journal", "metadata");

        protected override string OldJournal => "akka.persistence.journal.postgresql";

        protected override string NewJournal => "akka.persistence.journal.sql";
    }
}
