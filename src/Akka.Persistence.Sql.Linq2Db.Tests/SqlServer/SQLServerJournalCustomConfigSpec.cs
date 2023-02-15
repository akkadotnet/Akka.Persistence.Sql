using System.Threading.Tasks;
using Akka.Persistence.Linq2Db.Tests.Common;
using Akka.Persistence.TCK.Journal;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Linq2Db.Tests.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Linq2Db.Tests.SqlServer
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class SqlServerJournalCustomConfigSpec : JournalSpec, IAsyncLifetime
    {
        private static Configuration.Config Configuration(TestFixture fixture)
            => Linq2DbJournalDefaultSpecConfig.GetCustomConfig(
                "customSpec",
                "customJournalTable", 
                "customMetadataTable",
                ProviderName.SqlServer2017, 
                fixture.ConnectionString(Database.SqlServer),
                true);
        
        private readonly TestFixture _fixture;
        
        public SqlServerJournalCustomConfigSpec(ITestOutputHelper output, TestFixture fixture)
            : base(Configuration(fixture), nameof(SqlServerJournalCustomConfigSpec), output)
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