using System.Threading.Tasks;
using Akka.Persistence.Linq2Db.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Sqlite.Compatibility
{
    [Collection("PersistenceSpec")]
    public class SqliteSqlCommonSnapshotCompatibilitySpec : SqlCommonSnapshotCompatibilitySpec
    {
        private readonly TestFixture _fixture;
        
        public SqliteSqlCommonSnapshotCompatibilitySpec(ITestOutputHelper outputHelper, TestFixture fixture) : base(outputHelper)
        {
            _fixture = fixture;
            Config = SqliteCompatibilitySpecConfig.InitSnapshotConfig(
                "snapshot_compat", fixture.ConnectionString(Database.MsSqLite));
        }

        protected override Configuration.Config Config { get; }
            

        protected override string OldSnapshot => "akka.persistence.snapshot-store.sqlite";

        protected override string NewSnapshot => "akka.persistence.snapshot-store.linq2db";

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await _fixture.InitializeDbAsync(Database.MsSqLite);
        }
    }
}