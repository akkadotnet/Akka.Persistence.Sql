// -----------------------------------------------------------------------
//  <copyright file="SqliteMigratorCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2023 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;


namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.Sqlite
{
    [Collection("SqlCompatSpec")]
    public class SqliteMigratorCompatibilitySpec: MigratorCompatibilitySpec<SqliteFixture>
    {
        public SqliteMigratorCompatibilitySpec(ITestOutputHelper output) : base(output)
        {
        }

        protected override TestSettings Settings => SqliteSpecSettings.Instance;
    }
}
