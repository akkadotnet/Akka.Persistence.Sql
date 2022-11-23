// -----------------------------------------------------------------------
//  <copyright file="SqlServerCompatibilityLogicalDeleteSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.SqlServer
{
    [Collection("SqlCompatSpec")]
    public class SqlServerCompatibilityLogicalDeleteSpec: DataCompatibilityLogicalDeleteSpec<SqlServerFixture>
    {
        public SqlServerCompatibilityLogicalDeleteSpec(ITestOutputHelper output) : base(output)
        {
        }

        protected override TestSettings Settings => SqlServerSpecSettings.Instance;
    }
}