using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Linq2Db.Tests.Common;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.TCK.Journal;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Postgres
{
    [Collection("PersistenceSpec")]
    public class DockerLinq2DbPostgreSqlJournalSpec : JournalSpec, IAsyncLifetime
    {
        public static Configuration.Config Configuration(TestFixture fixture)
        {
            return ConfigurationFactory.ParseString(@$"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.linq2db""
        linq2db {{
            class = ""{typeof(Linq2DbWriteJournal).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
            connection-string = ""{fixture.ConnectionString(Database.Postgres)}""
            provider-name = ""{ProviderName.PostgreSQL95}""
            use-clone-connection = true
            auto-initialize = true
        }}
    }}
}}");
        }
        
        private readonly TestFixture _fixture;
        
        public DockerLinq2DbPostgreSqlJournalSpec(ITestOutputHelper output, TestFixture fixture) 
            : base(Configuration(fixture), nameof(DockerLinq2DbPostgreSqlJournalSpec), output)
        {
            _fixture = fixture;
            //DebuggingHelpers.SetupTraceDump(output);
        }
            
        protected override bool SupportsSerialization => false;
    
        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.Postgres);
            Initialize();
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}