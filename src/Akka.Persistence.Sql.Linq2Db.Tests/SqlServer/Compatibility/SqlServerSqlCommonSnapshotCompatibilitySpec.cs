using System.Threading.Tasks;
using Akka.Persistence.Linq2Db.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.SqlServer.Compatibility
{
    [Collection("PersistenceSpec")]
    public class SqlServerSqlCommonSnapshotCompatibilitySpec : SqlCommonSnapshotCompatibilitySpec
    {
        private readonly TestFixture _fixture;
        
        public SqlServerSqlCommonSnapshotCompatibilitySpec(ITestOutputHelper output, TestFixture fixture) : base(output)
        {
            _fixture = fixture;
        }

        protected override string OldSnapshot => "akka.persistence.snapshot-store.sql-server";

        protected override string NewSnapshot => "akka.persistence.snapshot-store.linq2db";

        protected override Configuration.Config Config =>
            SqlServerCompatibilitySpecConfig.InitSnapshotConfig(_fixture, "snapshot_compat");

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await _fixture.InitializeDbAsync(Database.SqlServer);
        }
    }
}