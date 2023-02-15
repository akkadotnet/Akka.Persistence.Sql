//-----------------------------------------------------------------------
// <copyright file="SqliteCurrentEventsByPersistenceIdSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Linq2Db.Tests.Common;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Query;
using Akka.Persistence.TCK.Query;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Query.Sqlite
{
    [Collection("PersistenceSpec")]
    public class SqliteCurrentEventsByPersistenceIdSpec : CurrentEventsByPersistenceIdSpec, IAsyncLifetime
    {
        private static Configuration.Config Config(TestFixture fixture)
        {
            return ConfigurationFactory.ParseString($@"
akka.loglevel = INFO
akka.persistence {{
    journal {{
        plugin = ""akka.persistence.journal.linq2db""
        linq2db {{
            class = ""{typeof(Linq2DbWriteJournal).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.actor.default-dispatcher""
            provider-name = ""{ProviderName.SQLiteMS}""
            table-mapping = sqlite
            connection-string = ""{fixture.ConnectionString(Database.MsSqLite)}""
            refresh-interval = 1s
            auto-initialize = on
        }}
    }}
    query {{
        journal {{
            linq2db {{
                provider-name = ""{ProviderName.SQLiteMS}""
                connection-string = ""{fixture.ConnectionString(Database.MsSqLite)}""
                table-mapping = sqlite
                auto-initialize = on
            }}
        }}
    }}
}}
akka.test.single-expect-default = 10s")
                .WithFallback(Linq2DbPersistence.DefaultConfiguration);
        }

        private readonly TestFixture _fixture;
        
        public SqliteCurrentEventsByPersistenceIdSpec(ITestOutputHelper output, TestFixture fixture) : base(Config(fixture), nameof(SqliteCurrentEventsByPersistenceIdSpec), output)
        {
            _fixture = fixture;
        }
        
        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.MsSqLite);
            ReadJournal = Sys.ReadJournalFor<Linq2DbReadJournal>(Linq2DbReadJournal.Identifier);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
