// -----------------------------------------------------------------------
//  <copyright file="SqliteSqlCommonJournalCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite.Compatibility
{
    [Collection("PersistenceSpec")]
    public class SqliteSqlCommonJournalCompatibilitySpec : SqlCommonJournalCompatibilitySpec
    {
        private readonly TestFixture _fixture;

        public SqliteSqlCommonJournalCompatibilitySpec(
            ITestOutputHelper outputHelper,
            TestFixture fixture)
            : base(
                outputHelper)
        {
            _fixture = fixture;

            Config = SqliteCompatibilitySpecConfig.InitJournalConfig(
                "journal_compat",
                "journal_metadata_compat",
                _fixture.ConnectionString(Database.MsSqlite));
        }

        protected override string OldJournal
            => "akka.persistence.journal.sqlite";

        protected override string NewJournal
            => "akka.persistence.journal.sql";

        protected override Configuration.Config Config { get; }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await _fixture.InitializeDbAsync(Database.MsSqlite);
        }
    }
}
