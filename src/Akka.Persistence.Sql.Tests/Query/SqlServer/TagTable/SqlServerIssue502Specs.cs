// -----------------------------------------------------------------------
//  <copyright file="SqlServerIssue502Specs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.SqlServer;
using Xunit;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.Query.SqlServer.TagTable
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(SqlServerPersistenceSpec))]
    public class SqlServerIssue502Specs: Issue502SpecsBase<SqlServerContainer>
    {
        public SqlServerIssue502Specs(ITestOutputHelper output, SqlServerContainer fixture)
            : base(TagMode.TagTable, output, nameof(SqlServerQueryThrottleSpecs), fixture)
        {
        }
    }
}
