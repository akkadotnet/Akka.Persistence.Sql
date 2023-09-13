// -----------------------------------------------------------------------
//  <copyright file="MsSqliteEndToEndSpec.cs" company="Akka.NET Project">
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
    public class SqlServerEndToEndSpec: SqlEndToEndSpecBase<SqlServerContainer>
    {
        public SqlServerEndToEndSpec(ITestOutputHelper output, SqlServerContainer fixture) : base(output, fixture) { }
    }
}
