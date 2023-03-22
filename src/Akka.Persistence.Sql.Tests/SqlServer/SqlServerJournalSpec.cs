// -----------------------------------------------------------------------
//  <copyright file="SqlServerJournalSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Journal;
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
            => SqlServerJournalSpecConfig.Create(
                fixture.ConnectionString(Database.SqlServer),
                "journalSpec");
    }
}
