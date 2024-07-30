// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlBugfix344Spec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Query;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.Query.PostgreSql
{
    /// <summary>
    /// Need our own collection, to ensure that the database tables haven't been initialized yet
    /// </summary>
    [CollectionDefinition(nameof(PostgreSqlBugfix344Fixture), DisableParallelization = true)]
    public sealed class PostgreSqlBugfix344Fixture : ICollectionFixture<PostgreSqlContainer> { }
    
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(PostgreSqlBugfix344Fixture))]
    public class PostgreSqlBugfix344Spec : Bugfix344Spec<PostgreSqlContainer>
    {
        public PostgreSqlBugfix344Spec(ITestOutputHelper output, PostgreSqlContainer fixture) : base(output, fixture)
        {
            
        }
    }
}
