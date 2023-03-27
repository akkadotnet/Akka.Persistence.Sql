﻿// -----------------------------------------------------------------------
//  <copyright file="MsSqliteCurrentEventsByTagSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Query;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.MsSqlite.Csv
{
    [Collection("PersistenceSpec")]
    public class MsSqliteCurrentEventsByTagSpec : BaseCurrentEventsByTagSpec
    {
        public MsSqliteCurrentEventsByTagSpec(ITestOutputHelper output, TestFixture fixture) 
            : base(SqliteConfig.MsCsv, output, fixture) { }
    }
}