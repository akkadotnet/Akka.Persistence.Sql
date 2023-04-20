// -----------------------------------------------------------------------
//  <copyright file="MySqlCommonSnapshotCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.MySql.Compatibility
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(MySqlPersistenceSpec))]
    public class MySqlCommonSnapshotCompatibilitySpec : SqlCommonSnapshotCompatibilitySpec<MySqlContainer>
    {
        public MySqlCommonSnapshotCompatibilitySpec(ITestOutputHelper output, MySqlContainer fixture)
            : base(fixture, output) { }

        protected override string OldSnapshot => "akka.persistence.snapshot-store.mysql";

        protected override string NewSnapshot => "akka.persistence.snapshot-store.sql";

        protected override Func<MySqlContainer, Configuration.Config> Config => fixture
            => MySqlCompatibilitySpecConfig.InitSnapshotConfig(fixture, "snapshot_store");
    }
}
