// -----------------------------------------------------------------------
//  <copyright file="SqliteCurrentEventsByTagSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Query;
using Akka.Persistence.Sql.Tests.Sqlite;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.Sqlite.TagTable
{
    [Collection(nameof(SqlitePersistenceSpec))]
    public class SqliteCurrentEventsByTagSpec : BaseCurrentEventsByTagSpec<SqliteContainer>
    {
        public SqliteCurrentEventsByTagSpec(ITestOutputHelper output, SqliteContainer fixture) 
            : base(TagMode.TagTable, output, nameof(SqliteCurrentEventsByTagSpec), fixture) { }
    }
}
