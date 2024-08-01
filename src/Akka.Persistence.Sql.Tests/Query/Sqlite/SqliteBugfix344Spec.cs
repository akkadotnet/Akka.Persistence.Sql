// -----------------------------------------------------------------------
//  <copyright file="SqliteBugfix344Spec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Query;
using Akka.Persistence.Sql.Tests.Sqlite;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.Query.Sqlite
{
    [Collection(nameof(SqlitePersistenceSpec))]
    public class SqliteBugfix344Spec : Bugfix344Spec<SqliteContainer>
    {
        public SqliteBugfix344Spec(ITestOutputHelper output, SqliteContainer fixture) 
            : base(output, fixture)
        {
        }
    }
}
