// -----------------------------------------------------------------------
//  <copyright file="SqliteSqlCommonSnapshotCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite.Compatibility
{
    [Collection("PersistenceSpec")]
    public class SqliteSqlCommonSnapshotCompatibilitySpec : SqlCommonSnapshotCompatibilitySpec
    {
        private readonly TestFixture _fixture;

        public SqliteSqlCommonSnapshotCompatibilitySpec(
            ITestOutputHelper outputHelper,
            TestFixture fixture)
            : base(
                outputHelper)
        {
            _fixture = fixture;

            Config = SqliteCompatibilitySpecConfig.InitSnapshotConfig(
                "snapshot_compat",
                fixture.ConnectionString(Database.MsSqlite));
        }

        protected override Configuration.Config Config { get; }

        protected override string OldSnapshot
            => "akka.persistence.snapshot-store.sqlite";

        protected override string NewSnapshot
            => "akka.persistence.snapshot-store.sql";

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await _fixture.InitializeDbAsync(Database.MsSqlite);
        }
    }
}
