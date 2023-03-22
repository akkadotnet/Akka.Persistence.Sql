// -----------------------------------------------------------------------
//  <copyright file="SqlServerCurrentAllEventsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Query;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Query.Csv.Tests.SqlServer
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class SqlServerCurrentAllEventsSpec : BaseCurrentAllEventsSpec
    {
        public SqlServerCurrentAllEventsSpec(ITestOutputHelper output, TestFixture fixture) 
            : base(SqlServerConfig.Csv, output, fixture) { }
    }
}
