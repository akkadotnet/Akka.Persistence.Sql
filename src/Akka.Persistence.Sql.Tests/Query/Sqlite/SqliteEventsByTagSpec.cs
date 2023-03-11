﻿//-----------------------------------------------------------------------
// <copyright file="SqliteEventsByTagSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.TCK.Query;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.Sqlite
{
    [Collection("PersistenceSpec")]
    public class SqliteEventsByTagSpec : EventsByTagSpec, IAsyncLifetime
    {
        private static Configuration.Config Config(TestFixture fixture)
        {
            return ConfigurationFactory.ParseString($@"
akka.loglevel = INFO
akka.persistence.journal.plugin = ""akka.persistence.journal.linq2db""
akka.persistence.journal.linq2db {{
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
akka.persistence.query.journal.linq2db
{{
    provider-name = ""{ProviderName.SQLiteMS}""
    table-mapping = sqlite
    connection-string = ""{fixture.ConnectionString(Database.MsSqlite)}""
    auto-initialize = on
}}
akka.test.single-expect-default = 10s")
                .WithFallback(Linq2DbPersistence.DefaultConfiguration);
        }

        private readonly TestFixture _fixture;

        public SqliteEventsByTagSpec(ITestOutputHelper output, TestFixture fixture) : base(Config(fixture), nameof(SqliteEventsByTagSpec), output)
        {
            _fixture = fixture;
        }

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.MsSqlite);
            ReadJournal = Sys.ReadJournalFor<Linq2DbReadJournal>(Linq2DbReadJournal.Identifier);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}
