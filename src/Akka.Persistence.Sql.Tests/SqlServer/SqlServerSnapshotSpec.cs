using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Snapshot;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.SqlServer
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class SqlServerSnapshotSpec : SnapshotStoreSpec, IAsyncLifetime
    {
        private static Configuration.Config Configuration(TestFixture fixture)
            => SqlServerSnapshotSpecConfig.Create(fixture.ConnectionString(Database.SqlServer),"snapshotSpec");

        private readonly TestFixture _fixture;

        public SqlServerSnapshotSpec(ITestOutputHelper output, TestFixture fixture)
            : base(Configuration(fixture), nameof(SqlServerSnapshotSpec), output)
        {
            _fixture = fixture;
            //DebuggingHelpers.SetupTraceDump(output);
        }

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.SqlServer);
            Initialize();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
