// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlJournalSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
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
        private readonly PostgreSqlContainer _fixture;

        public PostgreSqlJournalSpec(ITestOutputHelper output, PostgreSqlContainer fixture)
            : base(Configuration(fixture), nameof(PostgreSqlJournalSpec), output)
        {
            _fixture = fixture;
            Initialize();
        }

        protected override bool SupportsSerialization => false;

        protected override void AfterAll()
        {
            base.AfterAll();
            Shutdown();
            if (!_fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
        }

        public static Configuration.Config Configuration(PostgreSqlContainer fixture)
            => @$"
                akka.persistence {{
                    publish-plugin-commands = on
                    journal {{
                        plugin = ""akka.persistence.journal.sql""
                        sql {{
                            class = ""{typeof(SqlWriteJournal).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            connection-string = ""{fixture.ConnectionString}""
                            provider-name = ""{ProviderName.PostgreSQL95}""
                            use-clone-connection = true
                            auto-initialize = true
                        }}
                    }}
                }}";
    }
}
