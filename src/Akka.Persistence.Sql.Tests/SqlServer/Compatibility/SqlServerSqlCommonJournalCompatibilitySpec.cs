// -----------------------------------------------------------------------
//  <copyright file="SqlServerSqlCommonJournalCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.SqlServer.Compatibility
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(SqlServerPersistenceSpec))]
    public class SqlServerSqlCommonJournalCompatibilitySpec : SqlCommonJournalCompatibilitySpec<SqlServerContainer>
    {
        public SqlServerSqlCommonJournalCompatibilitySpec(ITestOutputHelper outputHelper, SqlServerContainer fixture)
            : base(fixture, outputHelper)
        {
        }

        protected override string OldJournal => "akka.persistence.journal.sql-server";

        protected override string NewJournal => "akka.persistence.journal.sql";

        protected override Func<SqlServerContainer, Configuration.Config> Config => fixture
            => SqlServerCompatibilitySpecConfig.InitJournalConfig(fixture, "journal_compat", "journal_metadata_compat");
    }
}
