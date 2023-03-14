// -----------------------------------------------------------------------
//  <copyright file="SpecCollectionFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common;
using Xunit;

namespace Akka.Persistence.Sql.Tests
{
    [CollectionDefinition("PersistenceSpec")]
    public sealed class SpecCollectionFixture : ICollectionFixture<TestFixture> { }
}
