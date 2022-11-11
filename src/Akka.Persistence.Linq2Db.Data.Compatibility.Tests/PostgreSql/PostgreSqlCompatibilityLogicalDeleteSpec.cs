// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlCompatibilityLogicalDeleteSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.PostgreSql
{
    [Collection("SqlCompatSpec")]
    public class PostgreSqlCompatibilityLogicalDeleteSpec: DataCompatibilityLogicalDeleteSpec<PostgreSqlFixture>
    {
        public PostgreSqlCompatibilityLogicalDeleteSpec(ITestOutputHelper output) : base(output)
        {
        }

        protected override TestSettings Settings => PostgreSqlSpecSettings.Instance;
    }
}