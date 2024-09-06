// -----------------------------------------------------------------------
//  <copyright file="SqlServerDataOptionsEndToEndSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.SqlServer
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(SqlServerPersistenceSpec))]
    public class SqlServerDataOptionsEndToEndSpec: SqlDataOptionsEndToEndSpecBase<SqlServerContainer>
    {
        public SqlServerDataOptionsEndToEndSpec(ITestOutputHelper output, SqlServerContainer fixture) 
            : base(nameof(SqlServerDataOptionsEndToEndSpec), output, fixture) { }
    }
}
