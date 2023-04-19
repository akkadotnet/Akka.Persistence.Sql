// -----------------------------------------------------------------------
//  <copyright file="MySqlSnapshotSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Snapshot;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.MySql
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(MySqlPersistenceSpec))]
    public class MySqlSnapshotSpec : SnapshotStoreSpec
    {
        public MySqlSnapshotSpec(ITestOutputHelper output, MySqlContainer fixture) :
            base(Configuration(fixture), nameof(MySqlSnapshotSpec), output)
        {
            Initialize();
        }

        private static Configuration.Config Configuration(MySqlContainer fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            return ConfigurationFactory.ParseString(
                    @$"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.sql""
        sql {{
            connection-string = ""{fixture.ConnectionString}""
            provider-name = ""{fixture.ProviderName}""
        }}
    }}
    snapshot-store {{
        plugin = ""akka.persistence.snapshot-store.sql""
        sql {{
            connection-string = ""{fixture.ConnectionString}""
            provider-name = ""{fixture.ProviderName}""
        }}
    }}
}}")
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }
    }
}
