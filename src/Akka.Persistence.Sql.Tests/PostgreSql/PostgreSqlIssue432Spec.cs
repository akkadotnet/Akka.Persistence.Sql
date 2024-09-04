// -----------------------------------------------------------------------
//  <copyright file="MySqlIssue432Spec.cs" company="Akka.NET Project">
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
    public class PostgreSqlIssue432Spec: Issue432SpecBase<PostgreSqlContainer>
    {
        public PostgreSqlIssue432Spec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(nameof(PostgreSqlIssue432Spec), fixture, output)
        {
        }
    }
}
