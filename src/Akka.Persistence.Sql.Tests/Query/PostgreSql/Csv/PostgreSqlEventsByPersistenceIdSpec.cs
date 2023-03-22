// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlEventsByPersistenceIdSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Query.Base;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.PostgreSql.Csv
{
    [Collection("PersistenceSpec")]
    public class PostgreSqlEventsByPersistenceIdSpec : BaseEventsByPersistenceIdSpec
    {
        public PostgreSqlEventsByPersistenceIdSpec(ITestOutputHelper output, TestFixture fixture)
            : base(PostgreSqlConfig.Csv, output, fixture) { }
    }
}
