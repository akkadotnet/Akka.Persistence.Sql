// -----------------------------------------------------------------------
//  <copyright file="SqlitePersistenceIdsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Query;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.Sqlite
{
    [Collection("PersistenceSpec")]
    public class SqlitePersistenceIdsSpec : PersistenceIdsSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public SqlitePersistenceIdsSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                Config(fixture),
                nameof(SqlitePersistenceIdsSpec),
                output)
            => _fixture = fixture;

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.MsSqlite);
            ReadJournal = Sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
        }

        public Task DisposeAsync()
            => Task.CompletedTask;

        private static Configuration.Config Config(TestFixture fixture)
            => ConfigurationFactory.ParseString(
                    $@"
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
                            plugin = ""akka.persistence.journal.sql""
                            sql = {{
                                provider-name = ""{ProviderName.SQLiteMS}""
                                table-mapping = sqlite
                                plugin-dispatcher = ""akka.actor.default-dispatcher""
                                auto-initialize = on
                                connection-string = ""{fixture.ConnectionString(Database.MsSqlite)}""
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
                                connection-string = ""{fixture.ConnectionString(Database.MsSqlite)}""
                            }}
                        }}
                    }}
                    akka.persistence.query.journal.sql {{
                        provider-name = ""{ProviderName.SQLiteMS}""
                        table-mapping = sqlite
                        connection-string = ""{fixture.ConnectionString(Database.MsSqlite)}""
                        auto-initialize = on
                        write-plugin = ""akka.persistence.journal.sql""
                    }}
                    akka.test.single-expect-default = 10s")
                .WithFallback(SqlPersistence.DefaultConfiguration);
    }
}
