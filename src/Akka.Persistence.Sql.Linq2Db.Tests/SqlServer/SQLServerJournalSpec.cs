using System.Threading.Tasks;
using Akka.Persistence.Linq2Db.Tests.Common;
using Akka.Persistence.TCK.Journal;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.SqlServer
{
    [Collection("PersistenceSpec")]
    public class SqlServerJournalSpec : JournalSpec, IAsyncLifetime
    {
        private static Configuration.Config Configuration(TestFixture fixture)
            => SqlServerJournalSpecConfig.Create(fixture.ConnectionString(Database.SqlServer), "journalSpec");
        
        private readonly TestFixture _fixture;
        
        public SqlServerJournalSpec(ITestOutputHelper output, TestFixture fixture)
            : base(Configuration(fixture), nameof(SqlServerJournalSpec), output)
        {
            _fixture = fixture;
            //DebuggingHelpers.SetupTraceDump(output);
        }
        
        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;
        
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