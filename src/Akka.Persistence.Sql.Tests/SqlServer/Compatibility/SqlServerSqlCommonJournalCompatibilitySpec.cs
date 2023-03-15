// -----------------------------------------------------------------------
//  <copyright file="SqlServerSqlCommonJournalCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.SqlServer.Compatibility
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class SqlServerSqlCommonJournalCompatibilitySpec : SqlCommonJournalCompatibilitySpec
    {
        private readonly TestFixture _fixture;

        public SqlServerSqlCommonJournalCompatibilitySpec(
            ITestOutputHelper outputHelper,
            TestFixture fixture)
            : base(
                outputHelper)
        {
            _fixture = fixture;

            Config = SqlServerCompatibilitySpecConfig.InitJournalConfig(
                _fixture,
                "journal_compat",
                "journal_metadata_compat");
        }

        protected override string OldJournal
            => "akka.persistence.journal.sql-server";

        protected override string NewJournal
            => "akka.persistence.journal.linq2db";

        protected override Configuration.Config Config { get; }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await _fixture.InitializeDbAsync(Database.SqlServer);
        }
    }
}
