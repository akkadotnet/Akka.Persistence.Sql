// -----------------------------------------------------------------------
//  <copyright file="MySqlCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Hosting;
using Akka.Persistence.Sql.Linq2Db;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Snapshot;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.MySql
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