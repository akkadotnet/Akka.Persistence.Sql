// -----------------------------------------------------------------------
//  <copyright file="MySqlPersistenceSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;

namespace Akka.Persistence.Sql.Tests.MySql
{
    [CollectionDefinition(nameof(MySqlPersistenceSpec), DisableParallelization = true)]
    public sealed class MySqlPersistenceSpec : ICollectionFixture<MySqlContainer> { }
}
