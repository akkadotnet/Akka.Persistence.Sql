// -----------------------------------------------------------------------
//  <copyright file="SqlServerJournalDefaultConfigSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
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

namespace Akka.Persistence.Sql.Tests.SqlServer
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class SqlServerJournalDefaultConfigSpec : JournalSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public SqlServerJournalDefaultConfigSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                Configuration(fixture),
                nameof(SqlServerJournalDefaultConfigSpec),
                output)
            => _fixture = fixture;

        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;

        public Task InitializeAsync()
        {
            Initialize();
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            using var cts = new CancellationTokenSource(10.Seconds());
            await Task.WhenAny(
                Task.Delay(Timeout.Infinite, cts.Token),
                _fixture.InitializeDbAsync(Database.SqlServer));
            
            if(cts.IsCancellationRequested)
                throw new XunitException("Failed to clean up database in 10 seconds");
        }

        private static Configuration.Config Configuration(TestFixture fixture)
            => SqlJournalDefaultSpecConfig.GetConfig(
                "defaultJournalSpec",
                "defaultJournalMetadata",
                ProviderName.SqlServer2017,
                fixture.ConnectionString(Database.SqlServer));
    }
}
