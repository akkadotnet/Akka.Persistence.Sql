// -----------------------------------------------------------------------
//  <copyright file="SqliteAllEventsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Query;
using Akka.Util.Internal;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.Sqlite
{
    [Collection("PersistenceSpec")]
    public class SqliteAllEventsSpec : AllEventsSpec, IAsyncLifetime
    {
        public static readonly AtomicCounter Counter = new(0);

        private readonly TestFixture _fixture;

        public SqliteAllEventsSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                Config(fixture),
                nameof(SqliteAllEventsSpec),
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
                    akka.persistence.journal.plugin = ""akka.persistence.journal.sql""
                    akka.persistence.journal.sql {{
                        event-adapters {{
                            color-tagger  = ""Akka.Persistence.TCK.Query.ColorFruitTagger, Akka.Persistence.TCK""
                        }}
                        event-adapter-bindings = {{
                            ""System.String"" = color-tagger
                        }}
                        plugin-dispatcher = ""akka.actor.default-dispatcher""
                        provider-name = ""{ProviderName.SQLiteMS}""
                        table-mapping = sqlite
                        auto-initialize = on
                        connection-string = ""{fixture.ConnectionString(Database.MsSqlite)}""
                        refresh-interval = 1s
                    }}
                    akka.persistence.query.journal.sql {{
                        provider-name = ""{ProviderName.SQLiteMS}""
                        connection-string = ""{fixture.ConnectionString(Database.MsSqlite)}""
                        table-mapping = sqlite
                        auto-initialize = on
                    }}
                    akka.test.single-expect-default = 10s")
                .WithFallback(SqlPersistence.DefaultConfiguration);
    }
}
