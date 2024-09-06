// -----------------------------------------------------------------------
//  <copyright file="MsSqliteSnapshotStoreSaveSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(MsSqlitePersistenceSpec))]
    public class MsSqliteSnapshotStoreSaveSnapshotSpec: SnapshotStoreSaveSnapshotSpecBase
    {
        public MsSqliteSnapshotStoreSaveSnapshotSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(SqliteSnapshotSpecConfig.Create(fixture), nameof(MsSqliteSnapshotStoreSaveSnapshotSpec), output)
        {
        }
    }
}
