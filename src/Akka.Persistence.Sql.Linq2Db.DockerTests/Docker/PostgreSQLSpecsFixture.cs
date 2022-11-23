﻿using Xunit;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker
{
    [CollectionDefinition("PostgreSQLSpec")]
    public sealed class PostgreSqlSpecsFixture : ICollectionFixture<PostgreSqlFixture>
    {
    }
}