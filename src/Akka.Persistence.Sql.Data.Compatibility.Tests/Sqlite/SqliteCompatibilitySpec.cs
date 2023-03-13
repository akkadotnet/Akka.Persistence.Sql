// -----------------------------------------------------------------------
//  <copyright file="SqliteCompatibilitySpec.cs" company="Akka.NET Project">
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

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.Sqlite
{
    [Collection("SqlCompatSpec")]
    public class SqliteCompatibilitySpec: DataCompatibilitySpec<SqliteFixture>
    {
        public SqliteCompatibilitySpec(ITestOutputHelper helper): base(helper)
        {
        }

        protected override TestSettings Settings => SqliteSpecSettings.Instance;
    }
}
