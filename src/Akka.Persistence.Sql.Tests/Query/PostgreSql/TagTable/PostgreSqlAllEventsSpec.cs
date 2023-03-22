// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlAllEventsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Query;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.PostgreSql.TagTable
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class PostgreSqlAllEventsSpec : BaseAllEventsSpec
    {
        public PostgreSqlAllEventsSpec(ITestOutputHelper output, TestFixture fixture) 
            : base(PostgreSqlConfig.TagTable, output, fixture) { }
    }
}
