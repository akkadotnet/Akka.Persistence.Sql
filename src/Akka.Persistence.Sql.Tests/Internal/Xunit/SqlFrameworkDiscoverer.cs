// -----------------------------------------------------------------------
//  <copyright file="SqlFrameworkDiscoverer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Akka.Persistence.Sql.Tests.Internal.Xunit
{
    public class SqlFrameworkDiscoverer : XunitTestFrameworkDiscoverer
    {
        public SqlFrameworkDiscoverer(
            IAssemblyInfo assemblyInfo,
            ISourceInformationProvider sourceProvider,
            IMessageSink diagnosticMessageSink,
            IXunitTestCollectionFactory collectionFactory = null)
            : base(assemblyInfo, sourceProvider, diagnosticMessageSink, collectionFactory) { }

        protected override bool IsValidTestClass(ITypeInfo type)
        {
            var isUnix = Environment.OSVersion.Platform == PlatformID.Unix;
            var skipLinux = type.GetCustomAttributes(typeof(SkipLinuxAttribute)).Any() && isUnix;
            var skipWindows = type.GetCustomAttributes(typeof(SkipWindowsAttribute)).Any() && !isUnix;
            return !type.IsAbstract || type.IsSealed || skipLinux || skipWindows;
        }

        protected override bool FindTestsForType(
            ITestClass testClass,
            bool includeSourceInformation,
            IMessageBus messageBus,
            ITestFrameworkDiscoveryOptions discoveryOptions)
        {
            var isUnix = Environment.OSVersion.Platform == PlatformID.Unix;
            var skipLinux = testClass.Class.GetCustomAttributes(typeof(SkipLinuxAttribute)).Any() && isUnix;
            var skipWindows = testClass.Class.GetCustomAttributes(typeof(SkipWindowsAttribute)).Any() && !isUnix;

            return !skipLinux && !skipWindows && base.FindTestsForType(
                testClass,
                includeSourceInformation,
                messageBus,
                discoveryOptions);
        }
    }
}
