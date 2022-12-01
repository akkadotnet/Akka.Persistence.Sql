using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.CompatibilityTests.Docker.Postgres
{
    [Collection("PostgreSqlSpec")]
    public class PostgreSqlCommonJournalCompatibilitySpec : SqlCommonJournalCompatibilitySpec
    {
        protected override Config Config { get; }

        protected override string OldJournal =>
            "akka.persistence.journal.postgresql";

        protected override string NewJournal =>
            "akka.persistence.journal.linq2db";

        public PostgreSqlCommonJournalCompatibilitySpec(ITestOutputHelper output, PostgreSqlFixture fixture) 
            : base( output)
        {
            PostgreDbUtils.Initialize(fixture);
            Config = PostgreSqlCompatibilitySpecConfig.InitJournalConfig("event_journal", "metadata");
        }
    }
}