// -----------------------------------------------------------------------
//  <copyright file="SqliteEventsByPersistenceIdSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Query;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.Sqlite.TagTable
{
    [Collection("PersistenceSpec")]
    public class SqliteEventsByPersistenceIdSpec : BaseEventsByPersistenceIdSpec
    {
        public SqliteEventsByPersistenceIdSpec(ITestOutputHelper output, TestFixture fixture)
            : base(SqliteConfig.TagTable, output, fixture) { }
    }
}
