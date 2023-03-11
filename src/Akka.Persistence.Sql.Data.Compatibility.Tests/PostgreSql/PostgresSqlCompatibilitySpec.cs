// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlCompatibilitySpec.cs" company="Akka.NET Project">
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

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.PostgreSql
{
    [Collection("SqlCompatSpec")]
    public class PostgreSqlCompatibilitySpec: DataCompatibilitySpec<PostgreSqlFixture>
    {
        public PostgreSqlCompatibilitySpec(ITestOutputHelper helper): base(helper)
        {
        }

        protected override TestSettings Settings => PostgreSqlSpecSettings.Instance;
    }
}