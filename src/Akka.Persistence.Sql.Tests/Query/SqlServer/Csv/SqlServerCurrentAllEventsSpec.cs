// -----------------------------------------------------------------------
//  <copyright file="SqlServerCurrentAllEventsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Query;
using Akka.Persistence.Sql.Tests.SqlServer;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.Query.SqlServer.Csv
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(SqlServerPersistenceSpec))]
    public class SqlServerCurrentAllEventsSpec : BaseCurrentAllEventsSpec<SqlServerContainer>
    {
        public SqlServerCurrentAllEventsSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(TagMode.Csv, output, nameof(SqlServerCurrentAllEventsSpec), fixture) { }
    }
}
