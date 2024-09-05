// -----------------------------------------------------------------------
//  <copyright file="SystemDataSqliteSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Snapshot;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    [Collection(nameof(SqlitePersistenceSpec))]
    public class SystemDataSqliteSnapshotSpec: SnapshotStoreSpec
    {
        public SystemDataSqliteSnapshotSpec(ITestOutputHelper output, SqliteContainer fixture)
            : base(SqliteSnapshotSpecConfig.Create(fixture), nameof(SystemDataSqliteSnapshotSpec), output)
        {
            Initialize();
        }
    }
}
