// -----------------------------------------------------------------------
//  <copyright file="SqlServerCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.SqlServer
{
    [Collection("SqlCompatSpec")]
    public class SqlServerCompatibilitySpec: DataCompatibilitySpec<SqlServerFixture>
    {
        public SqlServerCompatibilitySpec(ITestOutputHelper helper): base(helper)
        {
        }

        protected override TestSettings Settings => SqlServerSpecSettings.Instance;
    }
}