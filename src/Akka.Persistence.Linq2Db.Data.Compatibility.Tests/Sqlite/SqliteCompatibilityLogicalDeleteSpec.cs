// -----------------------------------------------------------------------
//  <copyright file="SqliteCompatibilityLogicalDeleteSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.Sqlite
{
    [Collection("SqlCompatSpec")]
    public class SqliteCompatibilityLogicalDeleteSpec: DataCompatibilityLogicalDeleteSpec<SqliteFixture>
    {
        public SqliteCompatibilityLogicalDeleteSpec(ITestOutputHelper output) : base(output)
        {
        }

        protected override TestSettings Settings => SqliteSpecSettings.Instance;
    }
}