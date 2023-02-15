using System.Threading.Tasks;
using Akka.Persistence.Linq2Db.Tests.Common;
using Akka.Persistence.TCK.Journal;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Sqlite
{
    [Collection("PersistenceSpec")]
    public class SystemDataSqliteJournalSpec : JournalSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;
        
        public SystemDataSqliteJournalSpec(ITestOutputHelper output, TestFixture fixture)
            : base(
                SqLiteJournalSpecConfig.Create(fixture.ConnectionString(Database.SqLite), ProviderName.SQLiteClassic), 
                nameof(SystemDataSqliteJournalSpec), output)
        {
            _fixture = fixture;
        }
        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;
    
        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.SqLite);
            Initialize();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}