using System;
using System.Threading.Tasks;
using Akka.Persistence.Linq2Db.Tests.Common;
using Akka.Persistence.TCK.Snapshot;
using Akka.Util.Internal;
using LinqToDB;
using Microsoft.Data.Sqlite;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Sqlite
{
    [Collection("PersistenceSpec")]
    public class MsSqliteSnapshotSpec : SnapshotStoreSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;
        
        public MsSqliteSnapshotSpec(ITestOutputHelper outputHelper, TestFixture fixture) 
            : base( 
                SqLiteSnapshotSpecConfig.Create(fixture.ConnectionString(Database.MsSqLite), ProviderName.SQLiteMS),
                nameof(MsSqliteSnapshotSpec), outputHelper)
        {
            _fixture = fixture;
        }
        
        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;
    
        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.MsSqLite);
            Initialize();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}