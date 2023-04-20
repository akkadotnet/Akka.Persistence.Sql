// -----------------------------------------------------------------------
//  <copyright file="SystemDataSqliteJournalSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Journal;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    [Collection(nameof(SqlitePersistenceSpec))]
    public class SystemDataSqliteJournalSpec : JournalSpec
    {
        public SystemDataSqliteJournalSpec(ITestOutputHelper output, SqliteContainer fixture)
            : base(SqliteJournalSpecConfig.Create(fixture), nameof(SystemDataSqliteJournalSpec), output)
        {
            Initialize();
        }

        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;
    }
}
