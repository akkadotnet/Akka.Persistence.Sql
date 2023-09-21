// -----------------------------------------------------------------------
//  <copyright file="MsSqliteEndToEndSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    [Collection(nameof(MsSqlitePersistenceSpec))]
    public class MsSqliteEndToEndSpec: SqlEndToEndSpecBase<MsSqliteContainer>
    {
        public MsSqliteEndToEndSpec(ITestOutputHelper output, MsSqliteContainer fixture) : base(output, fixture) { }
    }
}
