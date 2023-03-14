// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlCommonJournalCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.PostgreSql.Compatibility
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection("PersistenceSpec")]
    public class PostgreSqlCommonJournalCompatibilitySpec : SqlCommonJournalCompatibilitySpec
    {
        private readonly TestFixture _fixture;

        public PostgreSqlCommonJournalCompatibilitySpec(
            ITestOutputHelper output,
            TestFixture fixture)
            : base(output)
        {
            _fixture = fixture;

            Config = PostgreSqlCompatibilitySpecConfig.InitJournalConfig(
                _fixture,
                "event_journal",
                "metadata");
        }

        protected override Configuration.Config Config { get; }

        protected override string OldJournal => "akka.persistence.journal.postgresql";

        protected override string NewJournal => "akka.persistence.journal.linq2db";

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await _fixture.InitializeDbAsync(Database.PostgreSql);
        }
    }
}
