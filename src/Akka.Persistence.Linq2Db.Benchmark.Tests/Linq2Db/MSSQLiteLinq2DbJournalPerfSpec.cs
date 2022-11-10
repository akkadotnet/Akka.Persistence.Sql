using System;
using Akka.Persistence.Sql.Linq2Db;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Tests;
using Akka.Util.Internal;
using LinqToDB;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.BenchmarkTests.Local.Linq2Db
{
    public class MsSqliteLinq2DbJournalPerfSpec : L2dbJournalPerfSpec
    {
        private static readonly AtomicCounter Counter = new AtomicCounter(0);
        
        private static readonly string ConnString = $"Filename=file:test-journal-{Counter.IncrementAndGet()}.db;Mode=Memory;Cache=Shared";

        private static readonly SqliteConnection HeldSqliteConnection = new SqliteConnection(ConnString);

        public static void InitWalForFileDb()
        {
            var c = new SqliteConnection(ConnString);
            c.Open();
            var walCommand = c.CreateCommand();
            walCommand.CommandText = @"
    PRAGMA journal_mode = 'wal'
";
            walCommand.ExecuteNonQuery();
        }
            
        public MsSqliteLinq2DbJournalPerfSpec(ITestOutputHelper output)
            : base(SqLiteJournalSpecConfig.Create(ConnString, ProviderName.SQLiteMS), "SqliteJournalSpec", output,eventsCount: TestConstants.NumMessages)
        {
            var extension = Linq2DbPersistence.Get(Sys);
            
            HeldSqliteConnection.Open();
            //InitWALForFileDb();
            var conf = new JournalConfig(
                SqLiteJournalSpecConfig.Create(ConnString, ProviderName.SQLiteMS)
                    .WithFallback(extension.DefaultConfig)
                    .GetConfig("akka.persistence.journal.linq2db"));
            
            var connFactory = new AkkaPersistenceDataConnectionFactory(conf);
            using var conn = connFactory.GetConnection();
            try
            {
                conn.GetTable<JournalRow>().Delete();
            }
            catch
            {
                // no-op
            }
        }
        
    }
}