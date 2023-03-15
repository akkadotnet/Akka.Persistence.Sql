// -----------------------------------------------------------------------
//  <copyright file="Linq2DbConfiguration.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Config
{
    public class Linq2DbConfiguration
    {
        public Linq2DbConfiguration(Configuration.Config config)
        {
            ProviderName = config.GetString("provider-name");
            ConnectionString = config.GetString("connection-string");
        }

        public string ConnectionString { get; }

        public string ProviderName { get; }
    }
}
