// -----------------------------------------------------------------------
//  <copyright file="PostgresSqlCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.PostgreSql
{
    [Collection("SqlCompatibilitySpec")]
    public class PostgreSqlCompatibilitySpec : DataCompatibilitySpec<PostgreSqlFixture>
    {
        public PostgreSqlCompatibilitySpec(ITestOutputHelper helper) : base(helper) { }

        protected override TestSettings Settings => PostgreSqlSpecSettings.Instance;
    }
}
