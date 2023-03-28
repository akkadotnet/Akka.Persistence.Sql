// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlAllEventsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Query;
using Akka.Persistence.Sql.Tests.PostgreSql;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.Query.PostgreSql.Csv
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(PostgreSqlPersistenceSpec))]
    public class PostgreSqlAllEventsSpec : BaseAllEventsSpec<PostgreSqlContainer>
    {
        public PostgreSqlAllEventsSpec(ITestOutputHelper output, PostgreSqlContainer fixture) 
            : base(TagMode.Csv, output, nameof(PostgreSqlAllEventsSpec), fixture) { }
    }
}
