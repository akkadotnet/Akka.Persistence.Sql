// -----------------------------------------------------------------------
//  <copyright file="MySqlCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Hosting;
using Akka.Persistence.Sql;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Snapshot;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.MySql
{
    [Collection("SqlCompatSpec")]
    public class MySqlCompatibilitySpec: DataCompatibilitySpec<MySqlFixture>
    {
        public MySqlCompatibilitySpec(ITestOutputHelper helper): base(helper)
        {
        }

        protected override TestSettings Settings => MySqlSpecSettings.Instance;
    }
}
