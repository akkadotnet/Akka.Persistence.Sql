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

        public SqlServerSqlCommonJournalCompatibilitySpec(ITestOutputHelper outputHelper, TestFixture fixture) : base(outputHelper)
        {
            _fixture = fixture;
        }

        protected override string OldJournal => "akka.persistence.journal.sql-server";

        protected override string NewJournal => "akka.persistence.journal.linq2db";

        protected override Configuration.Config Config =>
            SqlServerCompatibilitySpecConfig.InitJournalConfig(_fixture, "journal_compat", "journal_metadata_compat");

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await _fixture.InitializeDbAsync(Database.SqlServer);
        }
    }
}
