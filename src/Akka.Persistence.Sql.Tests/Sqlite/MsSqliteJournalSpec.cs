﻿// -----------------------------------------------------------------------
//  <copyright file="MsSqliteJournalSpec.cs" company="Akka.NET Project">
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

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.MsSqlite);
            Initialize();
        }

        public Task DisposeAsync()
            => Task.CompletedTask;
    }
}
