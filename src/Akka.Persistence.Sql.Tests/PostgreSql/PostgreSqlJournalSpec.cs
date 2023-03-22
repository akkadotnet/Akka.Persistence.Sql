// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlJournalSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Journal;
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
    [Collection("PersistenceSpec")]
    public class PostgreSqlJournalSpec : JournalSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public PostgreSqlJournalSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                Configuration(fixture),
                nameof(PostgreSqlJournalSpec),
                output)
            => _fixture = fixture;

        protected override bool SupportsSerialization => false;

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.PostgreSql);
            Initialize();
        }

        public Task DisposeAsync()
            => Task.CompletedTask;

        public static Configuration.Config Configuration(TestFixture fixture)
            => @$"
                akka.persistence {{
                    publish-plugin-commands = on
                    journal {{
                        plugin = ""akka.persistence.journal.sql""
                        sql {{
                            class = ""{typeof(SqlWriteJournal).AssemblyQualifiedName}""
                            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                            connection-string = ""{fixture.ConnectionString(Database.PostgreSql)}""
                            provider-name = ""{ProviderName.PostgreSQL95}""
                            use-clone-connection = true
                            auto-initialize = true
                        }}
                    }}
                }}";
    }
}
