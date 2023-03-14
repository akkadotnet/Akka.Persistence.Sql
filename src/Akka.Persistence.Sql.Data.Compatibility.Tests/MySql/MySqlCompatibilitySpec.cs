// -----------------------------------------------------------------------
//  <copyright file="MySqlCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.MySql
{
    [Collection("SqlCompatibilitySpec")]
    public class MySqlCompatibilitySpec : DataCompatibilitySpec<MySqlFixture>
    {
        public MySqlCompatibilitySpec(ITestOutputHelper helper) : base(helper) { }

        protected override TestSettings Settings => MySqlSpecSettings.Instance;
    }
}
