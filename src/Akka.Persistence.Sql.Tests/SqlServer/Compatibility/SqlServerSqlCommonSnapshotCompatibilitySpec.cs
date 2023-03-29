// -----------------------------------------------------------------------
//  <copyright file="SqlServerSqlCommonSnapshotCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.SqlServer.Compatibility
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(SqlServerPersistenceSpec))]
    public class SqlServerSqlCommonSnapshotCompatibilitySpec : SqlCommonSnapshotCompatibilitySpec<SqlServerContainer>
    {
        public SqlServerSqlCommonSnapshotCompatibilitySpec(
            ITestOutputHelper output,
            SqlServerContainer fixture)
            : base(fixture, output)
        {
        }

        protected override string OldSnapshot => "akka.persistence.snapshot-store.sql-server";

        protected override string NewSnapshot => "akka.persistence.snapshot-store.sql";

        protected override Func<SqlServerContainer, Configuration.Config> Config => fixture
            => SqlServerCompatibilitySpecConfig.InitSnapshotConfig(fixture, "snapshot_compat");
    }
}
