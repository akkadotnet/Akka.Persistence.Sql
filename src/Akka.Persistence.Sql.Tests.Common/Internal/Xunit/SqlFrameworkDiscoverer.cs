// -----------------------------------------------------------------------
//  <copyright file="SqlFrameworkDiscoverer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Reflection;
using System.Threading.Tasks;
using Xunit.Sdk;
using Xunit.v3;

namespace Akka.Persistence.Sql.Tests.Common.Internal.Xunit
{
    public class SqlFrameworkDiscoverer : XunitTestFrameworkDiscoverer
    {
        public SqlFrameworkDiscoverer(
            IXunitTestAssembly testAssembly,
            IXunitTestCollectionFactory? collectionFactory = null)
            : base(testAssembly, collectionFactory) { }

        protected override bool IsValidTestClass(Type type)
        {
            var isUnix = Environment.OSVersion.Platform == PlatformID.Unix;
            var skipLinux = type.GetCustomAttribute<SkipLinuxAttribute>() is not null && isUnix;
            var skipWindows = type.GetCustomAttribute<SkipWindowsAttribute>() is not null && !isUnix;
            return (!type.IsAbstract || type.IsSealed) && !skipLinux && !skipWindows;
        }

        protected override async ValueTask<bool> FindTestsForType(
            IXunitTestClass testClass,
            ITestFrameworkDiscoveryOptions discoveryOptions,
            Func<ITestCase, ValueTask<bool>> discoveryCallback)
        {
            var type = testClass.Class;
            var isUnix = Environment.OSVersion.Platform == PlatformID.Unix;
            var skipLinux = type.GetCustomAttribute<SkipLinuxAttribute>() is not null && isUnix;
            var skipWindows = type.GetCustomAttribute<SkipWindowsAttribute>() is not null && !isUnix;

            if (skipLinux || skipWindows)
                return true; // skip this class but continue discovery

            return await base.FindTestsForType(testClass, discoveryOptions, discoveryCallback);
        }
    }
}