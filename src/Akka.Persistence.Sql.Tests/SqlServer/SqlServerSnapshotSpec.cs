// -----------------------------------------------------------------------
//  <copyright file="SqlServerSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Snapshot;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.SqlServer
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(SqlServerPersistenceSpec))]
    public class SqlServerSnapshotSpec : SnapshotStoreSpec
    {
        private readonly SqlServerContainer _fixture;

        public SqlServerSnapshotSpec(
            ITestOutputHelper output,
            SqlServerContainer fixture)
            : base(
                Configuration(fixture),
                nameof(SqlServerSnapshotSpec),
                output)
        {
            _fixture = fixture;
            Initialize();
        }

        protected override void AfterAll()
        {
            base.AfterAll();
            Shutdown();
            if (!_fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
        }

        private static Configuration.Config Configuration(SqlServerContainer fixture)
            => SqlServerSnapshotSpecConfig.Create(fixture.ConnectionString, "snapshotSpec");
    }
}
