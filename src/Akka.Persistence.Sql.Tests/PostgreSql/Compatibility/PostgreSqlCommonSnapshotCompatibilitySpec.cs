// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlCommonSnapshotCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.PostgreSql.Compatibility
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(PostgreSqlPersistenceSpec))]
    public class PostgreSqlCommonSnapshotCompatibilitySpec : SqlCommonSnapshotCompatibilitySpec<PostgreSqlContainer>
    {
        public PostgreSqlCommonSnapshotCompatibilitySpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(fixture, output)
        {
            Config = PostgreSqlCompatibilitySpecConfig.InitSnapshotConfig(fixture, "snapshot_store");
        }

        protected override Configuration.Config Config { get; }

        protected override string OldSnapshot
            => "akka.persistence.snapshot-store.postgresql";

        protected override string NewSnapshot
            => "akka.persistence.snapshot-store.sql";
    }
}
