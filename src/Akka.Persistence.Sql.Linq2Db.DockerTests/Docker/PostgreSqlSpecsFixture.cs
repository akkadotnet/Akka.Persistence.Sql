// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlSpecsFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker
{
    [CollectionDefinition("PostgreSqlSpec")]
    public sealed class PostgreSqlSpecsFixture : ICollectionFixture<PostgreSqlFixture>
    {
    }
}