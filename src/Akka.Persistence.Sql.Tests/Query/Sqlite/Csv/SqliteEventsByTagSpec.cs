// -----------------------------------------------------------------------
//  <copyright file="SqliteEventsByTagSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Query;
using Akka.Persistence.Sql.Tests.Sqlite;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.Sqlite.Csv
{
    [Collection(nameof(SqlitePersistenceSpec))]
    public class SqliteEventsByTagSpec : BaseEventsByTagSpec<SqliteContainer>
    {
        public SqliteEventsByTagSpec(ITestOutputHelper output, SqliteContainer fixture)
            : base(TagMode.Csv, output, nameof(SqliteEventsByTagSpec), fixture) { }
    }
}
