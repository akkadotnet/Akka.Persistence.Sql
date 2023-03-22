﻿// -----------------------------------------------------------------------
//  <copyright file="SqliteCurrentEventsByPersistenceIdSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Query;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Query.Csv.Tests.Sqlite
{
    [Collection("PersistenceSpec")]
    public class SqliteCurrentEventsByPersistenceIdSpec : BaseCurrentEventsByPersistenceIdSpec
    {
        public SqliteCurrentEventsByPersistenceIdSpec(ITestOutputHelper output, TestFixture fixture) 
            : base(SqliteConfig.Csv, output, fixture) { }
    }
}