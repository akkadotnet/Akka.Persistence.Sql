// -----------------------------------------------------------------------
//  <copyright file="SqlServerSnapshotStoreSaveSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.SqlServer
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(SqlServerPersistenceSpec))]
    public class SqlServerSnapshotStoreSaveSnapshotSpec: SnapshotStoreSaveSnapshotSpecBase
    {
        public SqlServerSnapshotStoreSaveSnapshotSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(Configuration(fixture), nameof(SqlServerSnapshotStoreSaveSnapshotSpec), output)
        {
        }

        private static Configuration.Config Configuration(SqlServerContainer fixture)
            => SqlServerSnapshotSpecConfig.Create(fixture, "snapshotSpec");
        
    }
}
