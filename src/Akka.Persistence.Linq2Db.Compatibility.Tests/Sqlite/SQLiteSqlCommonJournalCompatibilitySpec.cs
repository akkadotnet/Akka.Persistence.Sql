using System;
using Akka.Persistence.Sql.Linq2Db.Tests;
using Akka.Util.Internal;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public class SqliteSqlCommonJournalCompatibilitySpec : SqlCommonJournalCompatibilitySpec
    {
        private static readonly AtomicCounter Counter = new AtomicCounter(0);
        private static readonly string ConnString = $"Filename=file:memdb-journal-{Counter.IncrementAndGet()}.db;Mode=Memory;Cache=Shared";
        private static readonly SqliteConnection HeldSqliteConnection = new SqliteConnection(ConnString);
        
        public SqliteSqlCommonJournalCompatibilitySpec(ITestOutputHelper outputHelper) : base(outputHelper)
        {
            //DebuggingHelpers.SetupTraceDump(outputHelper);
            {
                HeldSqliteConnection.Open();
            }
            //catch{}
            
            GC.KeepAlive(HeldSqliteConnection);
        }

        protected override string OldJournal =>
            "akka.persistence.journal.sqlite";

        protected override string NewJournal =>
            "akka.persistence.journal.linq2db";

        protected override Configuration.Config Config =>
            SqliteCompatibilitySpecConfig.InitJournalConfig("journal_compat", "journal_metadata_compat", ConnString);
    }
}