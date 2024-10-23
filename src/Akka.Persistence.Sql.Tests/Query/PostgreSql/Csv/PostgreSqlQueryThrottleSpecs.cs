// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlQueryThrottleSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.PostgreSql;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.PostgreSql.Csv;

[Collection(nameof(PostgreSqlPersistenceSpec))]
public class PostgreSqlQueryThrottleSpecs: QueryThrottleSpecsBase<PostgreSqlContainer>
{
    public PostgreSqlQueryThrottleSpecs(ITestOutputHelper output, PostgreSqlContainer fixture)
        : base(TagMode.Csv, output, nameof(PostgreSqlAllEventsSpec), fixture)
    {
    }
}
