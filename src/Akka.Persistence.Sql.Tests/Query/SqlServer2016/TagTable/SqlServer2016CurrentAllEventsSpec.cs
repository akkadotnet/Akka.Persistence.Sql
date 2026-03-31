// -----------------------------------------------------------------------
//  <copyright file="SqlServer2016CurrentAllEventsSpec.cs" company="Akka.NET Project">
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

namespace Akka.Persistence.Sql.Tests.Query.SqlServer2016.TagTable
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(SqlServer2016PersistenceSpec))]
    public class SqlServer2016CurrentAllEventsSpec : BaseCurrentAllEventsSpec<SqlServer2016Container>
    {
        public SqlServer2016CurrentAllEventsSpec(ITestOutputHelper output, SqlServer2016Container fixture)
            : base(TagMode.TagTable, output, nameof(SqlServer2016CurrentAllEventsSpec), fixture) { }
    }
}
