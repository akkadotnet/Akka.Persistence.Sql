﻿// -----------------------------------------------------------------------
//  <copyright file="SqlServerMigratorCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.SqlServer
{
    [Collection("SqlCompatibilitySpec")]
    public class SqlServerMigratorCompatibilitySpec : MigratorCompatibilitySpec<SqlServerFixture>
    {
        public SqlServerMigratorCompatibilitySpec(ITestOutputHelper output) : base(output) { }

        protected override TestSettings Settings => SqlServerSpecSettings.Instance;
    }
}
