// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlBugfix344Spec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Query;
using Akka.Persistence.Sql.Tests.PostgreSql;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.PostgreSql
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(PostgreSqlBugfix344Fixture))]
    public class PostgreSqlBugfix344Spec : Bugfix344Spec<PostgreSqlContainer>
    {
        public PostgreSqlBugfix344Spec(ITestOutputHelper output, PostgreSqlContainer fixture) : base(output, fixture)
        {
            
        }
    }
}
