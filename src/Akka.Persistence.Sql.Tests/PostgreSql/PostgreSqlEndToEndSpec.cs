// -----------------------------------------------------------------------
//  <copyright file="MsSqliteEndToEndSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.PostgreSql
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(PostgreSqlPersistenceSpec))]
    public class PostgreSqlEndToEndSpec: SqlEndToEndSpecBase<PostgreSqlContainer>
    {
        public PostgreSqlEndToEndSpec(ITestOutputHelper output, PostgreSqlContainer fixture) : base(output, fixture) { }
    }
}
