﻿// -----------------------------------------------------------------------
//  <copyright file="SqlServerCurrentEventsByTagSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Query;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.Query.SqlServer.TagTable
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class SqlServerCurrentEventsByTagSpec : BaseCurrentEventsByTagSpec
    {
        public SqlServerCurrentEventsByTagSpec(ITestOutputHelper output, TestFixture fixture) 
            : base(SqlServerConfig.TagTable, output, fixture) { }
    }
}
