// -----------------------------------------------------------------------
//  <copyright file="MySqlQueryThrottleSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.MySql;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.MySql.TagTable;

[Collection(nameof(MySqlPersistenceSpec))]
public class MySqlQueryThrottleSpecs: QueryThrottleSpecsBase<MySqlContainer>
{
    public MySqlQueryThrottleSpecs(ITestOutputHelper output, MySqlContainer fixture)
        : base(TagMode.TagTable, output, nameof(Csv.MySqlAllEventsSpec), fixture)
    {
    }
}
