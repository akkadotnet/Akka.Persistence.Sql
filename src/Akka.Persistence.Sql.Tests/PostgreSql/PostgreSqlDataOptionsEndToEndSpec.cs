// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlDataOptionsEndToEndSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.PostgreSql
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(PostgreSqlPersistenceSpec))]
    public class PostgreSqlDataOptionsEndToEndSpec: SqlDataOptionsEndToEndSpecBase<PostgreSqlContainer>
    {
        public PostgreSqlDataOptionsEndToEndSpec(ITestOutputHelper output, PostgreSqlContainer fixture) 
            : base(nameof(PostgreSqlDataOptionsEndToEndSpec), output, fixture) { }
    }
}
