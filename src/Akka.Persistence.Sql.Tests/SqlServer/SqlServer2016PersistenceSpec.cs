// -----------------------------------------------------------------------
//  <copyright file="SqlServer2016PersistenceSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;

namespace Akka.Persistence.Sql.Tests.SqlServer
{
    [CollectionDefinition(nameof(SqlServer2016PersistenceSpec), DisableParallelization = true)]
    public sealed class SqlServer2016PersistenceSpec : ICollectionFixture<SqlServer2016Container> { }
}
