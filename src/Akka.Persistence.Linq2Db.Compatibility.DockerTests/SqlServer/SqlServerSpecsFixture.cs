// -----------------------------------------------------------------------
//  <copyright file="SqlServerSpecsFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;
using Xunit;

namespace Akka.Persistence.Linq2Db.CompatibilityTests.Docker.SqlServer
{
    [CollectionDefinition("SqlServerSpec")]
    public sealed class SqlServerSpecsFixture : ICollectionFixture<SqlServerFixture>
    {
    }
}