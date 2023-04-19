// -----------------------------------------------------------------------
//  <copyright file="MsSqliteSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Snapshot;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    [Collection(nameof(MsSqlitePersistenceSpec))]
    public class MsSqliteSnapshotSpec : SnapshotStoreSpec
    {
        public MsSqliteSnapshotSpec(ITestOutputHelper outputHelper, MsSqliteContainer fixture)
            : base(SqliteSnapshotSpecConfig.Create(fixture), nameof(MsSqliteSnapshotSpec), outputHelper)
        {
            Initialize();
        }

        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;
    }
}
