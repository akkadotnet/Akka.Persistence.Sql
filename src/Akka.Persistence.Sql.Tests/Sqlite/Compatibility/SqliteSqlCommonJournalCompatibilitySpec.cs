// -----------------------------------------------------------------------
//  <copyright file="SqliteSqlCommonJournalCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite.Compatibility
{
    [Collection(nameof(MsSqlitePersistenceSpec))]
    public class SqliteSqlCommonJournalCompatibilitySpec : SqlCommonJournalCompatibilitySpec<MsSqliteContainer>
    {
        public SqliteSqlCommonJournalCompatibilitySpec(
            ITestOutputHelper outputHelper,
            MsSqliteContainer fixture)
            : base(fixture, outputHelper)
        {
            Config = SqliteCompatibilitySpecConfig.InitJournalConfig(
                "journal_compat", "journal_metadata_compat", fixture.ConnectionString);
        }

        protected override string OldJournal
            => "akka.persistence.journal.sqlite";

        protected override string NewJournal
            => "akka.persistence.journal.sql";

        protected override Configuration.Config Config { get; }
    }
}
