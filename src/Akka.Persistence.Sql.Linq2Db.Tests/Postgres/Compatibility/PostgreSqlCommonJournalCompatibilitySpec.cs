using System.Threading.Tasks;
using Akka.Persistence.Linq2Db.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Postgres.Compatibility
{
    [Collection("PersistenceSpec")]
    public class PostgreSqlCommonJournalCompatibilitySpec : SqlCommonJournalCompatibilitySpec
    {
        private readonly TestFixture _fixture;
        
        protected override Configuration.Config Config { get; }

        protected override string OldJournal => "akka.persistence.journal.postgresql";

        protected override string NewJournal => "akka.persistence.journal.linq2db";

        public PostgreSqlCommonJournalCompatibilitySpec(ITestOutputHelper output, TestFixture fixture) 
            : base( output)
        {
            _fixture = fixture;
            Config = PostgreSqlCompatibilitySpecConfig.InitJournalConfig(_fixture, "event_journal", "metadata");
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await _fixture.InitializeDbAsync(Database.Postgres);
        }
    }
}