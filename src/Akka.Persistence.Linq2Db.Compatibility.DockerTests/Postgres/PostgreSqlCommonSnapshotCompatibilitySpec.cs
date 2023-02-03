// //-----------------------------------------------------------------------
// // <copyright file="DockerLinq2DbPostgreSqlSqlCommonSnapshotCompatibilitySpec.cs" company="Akka.NET Project">
// //     Copyright (C) 2009-2020 Lightbend Inc. <http://www.lightbend.com>
// //     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// // </copyright>
// //-----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.PostgreSql.Snapshot;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Snapshot;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.CompatibilityTests.Docker.Postgres
{
    [Collection("PostgreSqlSpec")]
    public class PostgreSqlCommonSnapshotCompatibilitySpec : SqlCommonSnapshotCompatibilitySpec
    {
        protected override Config Config => PostgreSqlCompatibilitySpecConfig.InitSnapshotConfig("snapshot_store");

        protected override string OldSnapshot =>
            "akka.persistence.snapshot-store.postgresql";

        protected override string NewSnapshot =>
            "akka.persistence.snapshot-store.linq2db";

        public PostgreSqlCommonSnapshotCompatibilitySpec(ITestOutputHelper output, PostgreSqlFixture fixture) 
            : base( output)
        {
            PostgreDbUtils.Initialize(fixture);
        }
    }
}