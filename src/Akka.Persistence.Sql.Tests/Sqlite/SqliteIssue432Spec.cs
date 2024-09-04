// -----------------------------------------------------------------------
//  <copyright file="MySqlIssue432Spec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(SqlitePersistenceSpec))]
    public class SqliteIssue432Spec: Issue432SpecBase<SqliteContainer>
    {
        public SqliteIssue432Spec(ITestOutputHelper output, SqliteContainer fixture)
            : base(nameof(SqliteIssue432Spec), fixture, output)
        {
        }
    }
}
