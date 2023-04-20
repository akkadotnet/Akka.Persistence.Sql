// -----------------------------------------------------------------------
//  <copyright file="MySqlCommonJournalCompatibilitySpec.cs" company="Akka.NET Project">
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

namespace Akka.Persistence.Sql.Tests.MySql.Compatibility
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(MySqlPersistenceSpec))]
    public class MySqlCommonJournalCompatibilitySpec : SqlCommonJournalCompatibilitySpec<MySqlContainer>
    {
        public MySqlCommonJournalCompatibilitySpec(ITestOutputHelper output, MySqlContainer fixture)
            : base(fixture, output) { }

        protected override Func<MySqlContainer, Configuration.Config> Config => fixture
            => MySqlCompatibilitySpecConfig.InitJournalConfig(fixture, "event_journal", "metadata");

        protected override string OldJournal => "akka.persistence.journal.mysql";

        protected override string NewJournal => "akka.persistence.journal.sql";
    }
}
