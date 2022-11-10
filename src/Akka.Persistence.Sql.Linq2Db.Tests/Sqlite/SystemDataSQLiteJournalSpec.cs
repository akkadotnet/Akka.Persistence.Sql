using System;
using System.Data.SQLite;
using Akka.Util.Internal;
using LinqToDB;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public class SystemDataSqliteJournalSpec : Akka.Persistence.TCK.Journal.JournalSpec
    {
        private static readonly AtomicCounter Counter = new AtomicCounter(0);
        private static readonly string ConnString = $"FullUri=file:memdb{Counter.IncrementAndGet()}?mode=memory&cache=shared";
        private static readonly SQLiteConnection HeldSqliteConnection = new SQLiteConnection(ConnString);

        public SystemDataSqliteJournalSpec(ITestOutputHelper outputHelper) 
            : base(SqLiteJournalSpecConfig.Create(ConnString, ProviderName.SQLiteClassic), "linq2dbJournalSpec", output: outputHelper)
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