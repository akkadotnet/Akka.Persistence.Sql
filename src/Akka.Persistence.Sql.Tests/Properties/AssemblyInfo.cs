// -----------------------------------------------------------------------
//  <copyright file="AssemblyInfo.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit;

[assembly:
    TestFramework(
        "Akka.Persistence.Sql.Tests.Common.Internal.Xunit.SqlTestFramework",
        "Akka.Persistence.Sql.Tests.Common")]
