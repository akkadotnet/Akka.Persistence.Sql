// -----------------------------------------------------------------------
//  <copyright file="FixtureMode.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2023 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Tests.Common
{
    public enum Database
    {
        SqlServer,
        PostgreSql,
        MySql,
        Sqlite,
        MsSqlite
    }
}
