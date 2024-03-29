﻿// -----------------------------------------------------------------------
//  <copyright file="MySqlMigratorCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.MySql
{
    [Collection("SqlCompatibilitySpec")]
    public class MySqlMigratorCompatibilitySpec : MigratorCompatibilitySpec<MySqlFixture>
    {
        public MySqlMigratorCompatibilitySpec(ITestOutputHelper output) : base(output) { }

        protected override TestSettings Settings => MySqlSpecSettings.Instance;
    }
}
