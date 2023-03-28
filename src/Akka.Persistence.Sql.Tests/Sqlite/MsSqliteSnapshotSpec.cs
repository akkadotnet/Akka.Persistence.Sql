// -----------------------------------------------------------------------
//  <copyright file="MsSqliteSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Snapshot;
using FluentAssertions.Extensions;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    [Collection(nameof(MsSqlitePersistenceSpec))]
    public class MsSqliteSnapshotSpec : SnapshotStoreSpec
    {
        private readonly MsSqliteContainer _fixture;

        public MsSqliteSnapshotSpec(
            ITestOutputHelper outputHelper,
            MsSqliteContainer fixture)
            : base(
                SqliteSnapshotSpecConfig.Create(fixture.ConnectionString, ProviderName.SQLiteMS),
                nameof(MsSqliteSnapshotSpec),
                outputHelper)
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
