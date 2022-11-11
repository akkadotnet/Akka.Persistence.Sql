// -----------------------------------------------------------------------
//  <copyright file="MySqlCompatibilityLogicalDeleteSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.MySql
{
    [Collection("SqlCompatSpec")]
    public class MySqlCompatibilityLogicalDeleteSpec: DataCompatibilityLogicalDeleteSpec<MySqlFixture>
    {
        public MySqlCompatibilityLogicalDeleteSpec(ITestOutputHelper output) : base(output)
        {
        }

        protected override TestSettings Settings => MySqlSpecSettings.Instance;
        
    }
}