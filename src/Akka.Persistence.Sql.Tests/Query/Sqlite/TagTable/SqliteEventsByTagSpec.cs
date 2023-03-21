// -----------------------------------------------------------------------
//  <copyright file="SqliteEventsByTagSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Query.Base;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.Sqlite.TagTable
{
    [Collection("PersistenceSpec")]
    public class SqliteEventsByTagSpec : BaseEventsByTagSpec
    {
        public SqliteEventsByTagSpec(ITestOutputHelper output, TestFixture fixture)
            : base(SqliteConfig.TagTable, output, fixture) { }
    }
}
