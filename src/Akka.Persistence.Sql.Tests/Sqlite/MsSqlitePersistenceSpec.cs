// -----------------------------------------------------------------------
//  <copyright file="MsSqlitePersistenceSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    [CollectionDefinition(nameof(MsSqlitePersistenceSpec), DisableParallelization = true)]
    public sealed class MsSqlitePersistenceSpec : ICollectionFixture<MsSqliteContainer> { }
}
