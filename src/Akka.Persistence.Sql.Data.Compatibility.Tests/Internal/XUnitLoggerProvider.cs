// -----------------------------------------------------------------------
//  <copyright file="XUnitLoggerProvider.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.Internal
{
    public class XUnitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _helper;
        private readonly LogLevel _logLevel;

        public XUnitLoggerProvider(ITestOutputHelper helper, LogLevel logLevel)
        {
            _helper = helper;
            _logLevel = logLevel;
        }

        // no-op
        public void Dispose() { }

        public ILogger CreateLogger(string categoryName)
            => new XUnitLogger(categoryName, _helper, _logLevel);
    }
}
