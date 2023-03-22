// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlCurrentEventsByTagSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Query;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Query.Csv.Tests.PostgreSql
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class PostgreSqlCurrentEventsByTagSpec : BaseCurrentEventsByTagSpec
    {
        public PostgreSqlCurrentEventsByTagSpec(ITestOutputHelper output, TestFixture fixture) 
            : base(PostgreSqlConfig.Csv, output, fixture) { }
    }
}
