using System;
using Akka.Util.Internal;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public class MsSqliteNativeConfigSpec : MsSqliteJournalSpec
    {
        public MsSqliteNativeConfigSpec(ITestOutputHelper outputHelper) : base(outputHelper, true)
        {
        }
    }
    public class MsSqliteJournalSpec : Akka.Persistence.TCK.Journal.JournalSpec
    {
        private static readonly AtomicCounter Counter = new AtomicCounter(0);
        private static readonly string ConnString = $"Filename=file:memdb-journal-{Counter.IncrementAndGet()}.db;Mode=Memory;Cache=Shared";
        private static readonly SqliteConnection HeldSqliteConnection = new SqliteConnection(ConnString);

        public MsSqliteJournalSpec(ITestOutputHelper outputHelper, bool nativeMode = false) : base(SqLiteJournalSpecConfig.Create(ConnString, ProviderName.SQLiteMS, nativeMode),
            "linq2dbJournalSpec",
            output: outputHelper)
        {
            try
            {
                HeldSqliteConnection.Open();
            }
            catch
            {
                // no-op
            }
            Initialize();
            GC.KeepAlive(HeldSqliteConnection);
        }
        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;
    }
}