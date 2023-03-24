// -----------------------------------------------------------------------
//  <copyright file="MsSqliteJournalSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Journal;
using FluentAssertions.Extensions;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    [Collection("PersistenceSpec")]
    public class MsSqliteNativeConfigSpec : MsSqliteJournalSpec
    {
        public MsSqliteNativeConfigSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                output,
                fixture,
                nameof(MsSqliteNativeConfigSpec),
                true) { }
    }

    [Collection("PersistenceSpec")]
    public class MsSqliteJournalSpec : JournalSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public MsSqliteJournalSpec(
            ITestOutputHelper output,
            TestFixture fixture,
            string name = nameof(MsSqliteJournalSpec),
            bool nativeMode = false)
            : base(
                SqliteJournalSpecConfig.Create(
                    fixture.ConnectionString(Database.MsSqlite),
                    ProviderName.SQLiteMS,
                    nativeMode),
                name,
                output)
            => _fixture = fixture;

        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;

        public Task InitializeAsync()
        {
            Initialize();
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            using var cts = new CancellationTokenSource(10.Seconds());
            await Task.WhenAny(
                Task.Delay(Timeout.Infinite, cts.Token),
                _fixture.InitializeDbAsync(Database.MsSqlite));
            
            if(cts.IsCancellationRequested)
                throw new XunitException("Failed to clean up database in 10 seconds");
        }
    }
}
