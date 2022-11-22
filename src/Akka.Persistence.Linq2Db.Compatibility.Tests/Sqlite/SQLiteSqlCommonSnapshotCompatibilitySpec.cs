using System;
using Akka.Configuration;
using Akka.Util.Internal;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public class SqliteSqlCommonSnapshotCompatibilitySpec : SqlCommonSnapshotCompatibilitySpec
    {
        private static readonly AtomicCounter Counter = new AtomicCounter(0);
        //private static string  connString = "FullUri=file:memdb"+counter.IncrementAndGet() +"?mode=memory&cache=shared";
        private static readonly string ConnString = $"Filename=file:memdb-journal-{Counter.IncrementAndGet()}.db;Mode=Memory;Cache=Shared";
        private static readonly SqliteConnection HeldSqliteConnection =
            new SqliteConnection(ConnString);
        public SqliteSqlCommonSnapshotCompatibilitySpec(ITestOutputHelper outputHelper) : base(outputHelper)
        {
            {
                HeldSqliteConnection.Open();
            }
            //catch{}
            
            GC.KeepAlive(HeldSqliteConnection);
        }

        protected override Config Config { get; } =
            SqliteCompatibilitySpecConfig.InitSnapshotConfig("snapshot_compat", ConnString);

        protected override string OldSnapshot =>
            "akka.persistence.snapshot-store.sqlite";

        protected override string NewSnapshot =>
            "akka.persistence.snapshot-store.linq2db";
    }
}