// -----------------------------------------------------------------------
//  <copyright file="MySqlDataOptionsEndToEndSpec.cs" company="Akka.NET Project">
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
    public class MySqlDataOptionsEndToEndSpec: SqlDataOptionsEndToEndSpecBase<MySqlContainer>
    {
        public MySqlDataOptionsEndToEndSpec(ITestOutputHelper output, MySqlContainer fixture) 
            : base(nameof(MySqlDataOptionsEndToEndSpec), output, fixture) { }
    }
}
