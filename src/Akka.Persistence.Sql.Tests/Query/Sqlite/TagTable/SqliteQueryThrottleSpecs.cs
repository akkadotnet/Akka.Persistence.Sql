// -----------------------------------------------------------------------
//  <copyright file="SqliteQueryThrottleSpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Sqlite;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.Sqlite.TagTable;

[Collection(nameof(SqlitePersistenceSpec))]
public class SqliteQueryThrottleSpecs: QueryThrottleSpecsBase<SqliteContainer>
{
    public SqliteQueryThrottleSpecs(ITestOutputHelper output, SqliteContainer fixture)
        : base(TagMode.TagTable, output, nameof(Csv.SqliteAllEventsSpec), fixture)
    {
    }
}
