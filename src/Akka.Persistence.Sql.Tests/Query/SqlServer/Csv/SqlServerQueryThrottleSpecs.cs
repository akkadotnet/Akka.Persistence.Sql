// -----------------------------------------------------------------------
//  <copyright file="SqlServerQueryThrottleSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.SqlServer;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.SqlServer.Csv;

[Collection(nameof(SqlServerPersistenceSpec))]
public class SqlServerQueryThrottleSpecs: QueryThrottleSpecsBase<SqlServerContainer>
{
    public SqlServerQueryThrottleSpecs(ITestOutputHelper output, SqlServerContainer fixture)
        : base(TagMode.Csv, output, nameof(SqlServerAllEventsSpec), fixture)
    {
    }
}
