// -----------------------------------------------------------------------
//  <copyright file="MySqlCurrentEventsByTagSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Query;
using Akka.Persistence.Sql.Tests.MySql;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.Query.MySql.Csv
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(MySqlPersistenceSpec))]
    public class MySqlCurrentEventsByTagSpec : BaseCurrentEventsByTagSpec<MySqlContainer>
    {
        public MySqlCurrentEventsByTagSpec(ITestOutputHelper output, MySqlContainer fixture)
            : base(TagMode.Csv, output, nameof(MySqlAllEventsSpec), fixture) { }
    }
}
