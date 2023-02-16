//-----------------------------------------------------------------------
// <copyright file="SqlitePersistenceIdsSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Linq2Db.Tests.Common;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Linq2Db.Query;
using Akka.Persistence.TCK.Query;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Query.Sqlite
{
    [Collection("PersistenceSpec")]
    public class SqlitePersistenceIdsSpec : PersistenceIdsSpec, IAsyncLifetime
    {
        private static Configuration.Config Config(TestFixture fixture)
        {
                return ConfigurationFactory.ParseString($@"
akka.loglevel = INFO
akka.actor{{
    serializers{{
        persistence-tck-test=""Akka.Persistence.TCK.Serialization.TestSerializer,Akka.Persistence.TCK""
    }}
    serialization-bindings {{
        ""Akka.Persistence.TCK.Serialization.TestPayload,Akka.Persistence.TCK"" = persistence-tck-test
    }}
}}
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.linq2db""
        linq2db = {{
            provider-name = ""{ProviderName.SQLiteMS}""
            table-mapping = sqlite
            plugin-dispatcher = ""akka.actor.default-dispatcher""
            auto-initialize = on
            connection-string = ""{fixture.ConnectionString(Database.MsSqLite)}""
            refresh-interval = 200ms
        }}
    }}
    snapshot-store {{
        plugin = ""akka.persistence.snapshot-store.sqlite""
        sqlite {{
            class = ""Akka.Persistence.Sqlite.Snapshot.SqliteSnapshotStore, Akka.Persistence.Sqlite""
            plugin-dispatcher = ""akka.actor.default-dispatcher""
            table-name = snapshot_store
            auto-initialize = on
            connection-string = ""{fixture.ConnectionString(Database.MsSqLite)}""
        }}
    }}
}}
akka.persistence.query.journal.linq2db {{
    provider-name = ""{ProviderName.SQLiteMS}""
    table-mapping = sqlite
    connection-string = ""{fixture.ConnectionString(Database.MsSqLite)}""
    auto-initialize = on
    write-plugin = ""akka.persistence.journal.linq2db""
}}
akka.test.single-expect-default = 10s")
                    .WithFallback(Linq2DbPersistence.DefaultConfiguration);            
        }

        /*
        public override Task ReadJournal_should_deallocate_AllPersistenceIds_publisher_when_the_last_subscriber_left()
        {
            return Task.CompletedTask;
        }
        */

        private readonly TestFixture _fixture;
        
        public SqlitePersistenceIdsSpec(ITestOutputHelper output, TestFixture fixture) : base(Config(fixture),
            nameof(SqlitePersistenceIdsSpec), output)
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
