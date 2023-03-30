﻿// -----------------------------------------------------------------------
//  <copyright file="SqlServerSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Snapshot;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.SqlServer
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(SqlServerPersistenceSpec))]
    public class SqlServerSnapshotSpec : SnapshotStoreSpec
    {
        public SqlServerSnapshotSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(Configuration(fixture), nameof(SqlServerSnapshotSpec), output)
        {
            Initialize();
        }

        private static Configuration.Config Configuration(SqlServerContainer fixture)
            => SqlServerSnapshotSpecConfig.Create(fixture, "snapshotSpec");
    }
}
