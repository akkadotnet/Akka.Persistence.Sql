using System.Threading.Tasks;
using Akka.Persistence.Linq2Db.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Sqlite.Compatibility
{
    [Collection("PersistenceSpec")]
    public class SqliteSqlCommonJournalCompatibilitySpec : SqlCommonJournalCompatibilitySpec
    {
        private readonly TestFixture _fixture;
        
        public SqliteSqlCommonJournalCompatibilitySpec(ITestOutputHelper outputHelper, TestFixture fixture) : base(outputHelper)
        {
            _fixture = fixture;
            Config = SqliteCompatibilitySpecConfig.InitJournalConfig(
                "journal_compat", "journal_metadata_compat", _fixture.ConnectionString(Database.MsSqLite));
        }

        protected override string OldJournal => "akka.persistence.journal.sqlite";

        protected override string NewJournal => "akka.persistence.journal.linq2db";

        protected override Configuration.Config Config { get; }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await _fixture.InitializeDbAsync(Database.MsSqLite);
        }
    }
}