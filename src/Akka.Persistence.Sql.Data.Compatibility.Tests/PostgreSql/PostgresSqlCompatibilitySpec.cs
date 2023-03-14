// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlCompatibilitySpec.cs" company="Akka.NET Project">
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

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.PostgreSql
{
    [Collection("SqlCompatibilitySpec")]
    public class PostgreSqlCompatibilitySpec: DataCompatibilitySpec<PostgreSqlFixture>
    {
        public PostgreSqlCompatibilitySpec(ITestOutputHelper helper): base(helper)
        {
        }

        protected override TestSettings Settings => PostgreSqlSpecSettings.Instance;
    }
}
