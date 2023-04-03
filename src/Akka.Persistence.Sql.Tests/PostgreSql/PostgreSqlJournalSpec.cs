// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlJournalSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Journal;
using FluentAssertions.Extensions;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.PostgreSql
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(PostgreSqlPersistenceSpec))]
    public class PostgreSqlJournalSpec : JournalSpec
    {
        public PostgreSqlJournalSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(Configuration(fixture), nameof(PostgreSqlJournalSpec), output)
        {
            Initialize();
        }

        protected override bool SupportsSerialization => false;

        public static Configuration.Config Configuration(PostgreSqlContainer fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
            
            return ConfigurationFactory.ParseString(@$"
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.sql""
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
