// -----------------------------------------------------------------------
//  <copyright file="XUnitLoggerProvider.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Tests.Common.Internal
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

        public void Dispose()
        {
            // no-op
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XUnitLogger(categoryName, _helper, _logLevel);
        }
    }    
}

