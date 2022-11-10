using System;
using Akka.Persistence.TCK.Snapshot;
using Akka.Util.Internal;
using LinqToDB;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public class MsSqliteSnapshotSpec : SnapshotStoreSpec
    {
        private static readonly AtomicCounter Counter = new AtomicCounter(0);
        private static readonly string ConnString = $"Filename=file:memdb-journal-{Counter.IncrementAndGet()}.db;Mode=Memory;Cache=Shared";
        private static readonly SqliteConnection HeldSqliteConnection = new SqliteConnection(ConnString);

        public MsSqliteSnapshotSpec(ITestOutputHelper outputHelper) : base(SqLiteSnapshotSpecConfig.Create(ConnString, ProviderName.SQLiteMS),
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
            //DataConnection.OnTrace = info =>
            //{
            //    outputHelper.WriteLine(info.SqlText);
            //    if (info.Exception != null)
            //    {
            //        outputHelper.WriteLine(info.Exception.ToString());
            //    }
            //
            //    if (!string.IsNullOrWhiteSpace(info.CommandText))
            //    {
            //        outputHelper.WriteLine(info.CommandText);
            //    }
            //};
            Initialize();
            GC.KeepAlive(HeldSqliteConnection);
        }
        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;
    }
}