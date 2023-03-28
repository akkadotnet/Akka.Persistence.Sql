// -----------------------------------------------------------------------
//  <copyright file="MsSqliteJournalSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Journal;
using FluentAssertions.Extensions;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    [Collection(nameof(MsSqlitePersistenceSpec))]
    public class MsSqliteNativeConfigSpec : MsSqliteJournalSpec
    {
        public MsSqliteNativeConfigSpec(
            ITestOutputHelper output,
            MsSqliteContainer fixture)
            : base(
                output,
                fixture,
                nameof(MsSqliteNativeConfigSpec),
                true) { }
    }

    [Collection(nameof(MsSqlitePersistenceSpec))]
    public class MsSqliteJournalSpec : JournalSpec
    {
        private readonly MsSqliteContainer _fixture;

        public MsSqliteJournalSpec(
            ITestOutputHelper output,
            MsSqliteContainer fixture,
            string name = nameof(MsSqliteJournalSpec),
            bool nativeMode = false)
            : base(
                SqliteJournalSpecConfig.Create(fixture.ConnectionString, ProviderName.SQLiteMS, nativeMode),
                name,
                output)
        {
            _fixture = fixture;
            Initialize();
        }

        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;

        protected override void AfterAll()
        {
            base.AfterAll();
            Shutdown();
            if (!_fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
        }
    }
}
