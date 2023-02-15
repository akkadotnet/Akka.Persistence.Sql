// -----------------------------------------------------------------------
//  <copyright file="BenchmarkCollectionFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Linq2Db.Tests.Common;
using Xunit;

namespace Akka.Persistence.Linq2Db.Benchmark.Tests
{
    [CollectionDefinition("BenchmarkSpec")]
    public class BenchmarkCollectionFixture: ICollectionFixture<TestFixture>
    {
        
    }
}