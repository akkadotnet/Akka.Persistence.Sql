// -----------------------------------------------------------------------
//  <copyright file="SqlTestFramework.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Reflection;
using Xunit.v3;

namespace Akka.Persistence.Sql.Tests.Common.Internal.Xunit
{
    public class SqlTestFramework : XunitTestFramework
    {
        protected override ITestFrameworkDiscoverer CreateDiscoverer(Assembly assembly)
            => new SqlFrameworkDiscoverer(new XunitTestAssembly(assembly, version: assembly.GetName().Version));
    }
}
