// -----------------------------------------------------------------------
//  <copyright file="SqliteSqlCommonSnapshotCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite.Compatibility
{
    [Collection(nameof(MsSqlitePersistenceSpec))]
    public class SqliteSqlCommonSnapshotCompatibilitySpec : SqlCommonSnapshotCompatibilitySpec<MsSqliteContainer>
    {
        public SqliteSqlCommonSnapshotCompatibilitySpec(ITestOutputHelper outputHelper, MsSqliteContainer fixture)
            : base(fixture, outputHelper)
        {
            Config = SqliteCompatibilitySpecConfig.InitSnapshotConfig("snapshot_compat", fixture.ConnectionString);
        }

        protected override Configuration.Config Config { get; }

        protected override string OldSnapshot
            => "akka.persistence.snapshot-store.sqlite";

        protected override string NewSnapshot
            => "akka.persistence.snapshot-store.sql";
    }
}
