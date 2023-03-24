// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlJournalSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Journal;
using FluentAssertions.Extensions;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.PostgreSql
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class PostgreSqlJournalSpec : JournalSpec
    {
        private readonly TestFixture _fixture;

        public PostgreSqlJournalSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                Configuration(fixture),
                nameof(PostgreSqlJournalSpec),
                output)
        {
            _fixture = fixture;
            Initialize();
        }

        protected override bool SupportsSerialization => false;

        public Task InitializeAsync()
            => Task.CompletedTask;

        public async Task DisposeAsync()
        {
            using var cts = new CancellationTokenSource(10.Seconds());
            await Task.WhenAny(
                Task.Delay(Timeout.Infinite, cts.Token),
                _fixture.InitializeDbAsync(Database.PostgreSql));
            
            if(cts.IsCancellationRequested)
                throw new XunitException("Failed to clean up database in 10 seconds");
        }

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
