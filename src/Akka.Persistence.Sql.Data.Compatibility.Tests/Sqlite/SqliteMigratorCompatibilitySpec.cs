﻿// -----------------------------------------------------------------------
//  <copyright file="SqliteMigratorCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.Sqlite
{
    [Collection("SqlCompatibilitySpec")]
    public class SqliteMigratorCompatibilitySpec : MigratorCompatibilitySpec<SqliteFixture>
    {
        public SqliteMigratorCompatibilitySpec(ITestOutputHelper output) : base(output) { }

        protected override TestSettings Settings => SqliteSpecSettings.Instance;
    }
}
