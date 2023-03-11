﻿// //-----------------------------------------------------------------------
// // <copyright file="DockerLinq2DbPostgreSqlSqlCommonSnapshotCompatibilitySpec.cs" company="Akka.NET Project">
// //     Copyright (C) 2009-2020 Lightbend Inc. <http://www.lightbend.com>
// //     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// // </copyright>
// //-----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.PostgreSql.Compatibility
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class PostgreSqlCommonSnapshotCompatibilitySpec : SqlCommonSnapshotCompatibilitySpec
    {
        private readonly TestFixture _fixture;

        protected override Configuration.Config Config { get; }

        protected override string OldSnapshot => "akka.persistence.snapshot-store.postgresql";

        protected override string NewSnapshot => "akka.persistence.snapshot-store.linq2db";

        public PostgreSqlCommonSnapshotCompatibilitySpec(ITestOutputHelper output, TestFixture fixture)
            : base( output)
        {
            _fixture = fixture;
            Config = PostgreSqlCompatibilitySpecConfig.InitSnapshotConfig(_fixture, "snapshot_store");
        }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await _fixture.InitializeDbAsync(Database.PostgreSql);
        }
    }
}
