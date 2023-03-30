// -----------------------------------------------------------------------
//  <copyright file="SqlTestFramework.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Akka.Persistence.Sql.Tests.Common.Internal.Xunit
{
    public class SqlTestFramework : XunitTestFramework
    {
        public SqlTestFramework(IMessageSink messageSink) : base(messageSink) { }

        protected override ITestFrameworkDiscoverer CreateDiscoverer(IAssemblyInfo assemblyInfo)
            => new SqlFrameworkDiscoverer(assemblyInfo, SourceInformationProvider, DiagnosticMessageSink);
    }
}
