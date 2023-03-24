// -----------------------------------------------------------------------
//  <copyright file="SqlServerJournalSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Journal;
using FluentAssertions.Extensions;
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
    public class SqlServerJournalSpec : JournalSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public SqlServerJournalSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                Configuration(fixture),
                nameof(SqlServerJournalSpec),
                output)
        {
            _fixture = fixture;
            Initialize();
        }

        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;

        public Task InitializeAsync()
            => Task.CompletedTask;

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
            => SqlServerJournalSpecConfig.Create(
                fixture.ConnectionString(Database.SqlServer),
                "journalSpec");
    }
}
