// -----------------------------------------------------------------------
//  <copyright file="SqlServerJournalCustomConfigSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Journal;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.SqlServer
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class SqlServerJournalCustomConfigSpec : JournalSpec, IAsyncLifetime
    {
        private readonly TestFixture _fixture;

        public SqlServerJournalCustomConfigSpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(
                Configuration(fixture),
                nameof(SqlServerJournalCustomConfigSpec),
                output)
            => _fixture = fixture;

        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(Database.SqlServer);
            Initialize();
        }

        public Task DisposeAsync()
            => Task.CompletedTask;

        private static Configuration.Config Configuration(TestFixture fixture)
            => SqlJournalDefaultSpecConfig.GetCustomConfig(
                "customSpec",
                "customJournalTable",
                "customMetadataTable",
                ProviderName.SqlServer2017,
                fixture.ConnectionString(Database.SqlServer),
                true);
    }
}
