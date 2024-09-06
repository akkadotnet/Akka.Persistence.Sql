// -----------------------------------------------------------------------
//  <copyright file="SqliteSnapshotStoreSaveSnapshotSpec.cs" company="Akka.NET Project">
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
    [Collection(nameof(SqlitePersistenceSpec))]
    public class SqliteSnapshotStoreSaveSnapshotSpec: SnapshotStoreSaveSnapshotSpecBase
    {
        public SqliteSnapshotStoreSaveSnapshotSpec(ITestOutputHelper output, SqliteContainer fixture)
            : base(Configuration(fixture), nameof(SqliteSnapshotStoreSaveSnapshotSpec), output)
        {
        }

        private static Configuration.Config Configuration(SqliteContainer fixture)
            => SqliteSnapshotSpecConfig.Create(fixture);
        
    }
}
