// -----------------------------------------------------------------------
//  <copyright file="Linq2DbTestFramework.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Akka.Persistence.Sql.Tests.Internal.Xunit
{
    public class Linq2DbTestFramework : XunitTestFramework
    {
        public Linq2DbTestFramework(IMessageSink messageSink) : base(messageSink) { }

        protected override ITestFrameworkDiscoverer CreateDiscoverer(IAssemblyInfo assemblyInfo)
            => new Linq2DbFrameworkDiscoverer(assemblyInfo, SourceInformationProvider, DiagnosticMessageSink);
    }
}
