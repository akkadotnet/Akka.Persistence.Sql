// -----------------------------------------------------------------------
//  <copyright file="SystemDataSqliteJournalSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Journal;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    [Collection("PersistenceSpec")]
    public class SystemDataSqliteJournalSpec : JournalSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public SystemDataSqliteJournalSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                SqliteJournalSpecConfig.Create(fixture.ConnectionString(Database.Sqlite), ProviderName.SQLiteClassic),
                nameof(SystemDataSqliteJournalSpec),
                output)
            => _fixture = fixture;

        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.Sqlite);
            Initialize();
        }

        public Task DisposeAsync()
            => Task.CompletedTask;
    }
}
