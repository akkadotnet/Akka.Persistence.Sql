// -----------------------------------------------------------------------
//  <copyright file="MsSqliteEndToEndSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.MySql
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(MySqlPersistenceSpec))]
    public class MySqlEndToEndSpec: SqlEndToEndSpecBase<MySqlContainer>
    {
        public MySqlEndToEndSpec(ITestOutputHelper output, MySqlContainer fixture) : base(output, fixture) { }
    }
}
