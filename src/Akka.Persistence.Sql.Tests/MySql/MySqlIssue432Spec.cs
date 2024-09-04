// -----------------------------------------------------------------------
//  <copyright file="MySqlIssue432Spec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.MySql
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(MySqlPersistenceSpec))]
    public class MySqlIssue432Spec: Issue432SpecBase<MySqlContainer>
    {
        public MySqlIssue432Spec(ITestOutputHelper output, MySqlContainer fixture)
            : base(nameof(MySqlIssue432Spec), fixture, output)
        {
        }
    }
}
