// -----------------------------------------------------------------------
//  <copyright file="SpecCollectionFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;

namespace Akka.Persistence.Sql.Benchmark.Tests.SqlServer
{
    [CollectionDefinition(nameof(SqlServerPersistenceBenchmark), DisableParallelization = true)]
    public sealed class SqlServerPersistenceBenchmark : ICollectionFixture<SqlServerContainer> { }
}
