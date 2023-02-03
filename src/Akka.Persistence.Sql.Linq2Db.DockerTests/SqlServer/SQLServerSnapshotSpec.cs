using System;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Snapshot;
using Akka.Persistence.Sql.Linq2Db.Tests.Docker.Docker;
using Akka.Persistence.TCK.Snapshot;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Docker.SqlServer
{
    [Collection("SqlServerSpec")]
    public class SqlServerSnapshotSpec : SnapshotStoreSpec
    {
        public static Configuration.Config Initialize(SqlServerFixture fixture)
        {
            SqlServerDbUtils.Initialize(fixture.ConnectionString);
            return Configuration;
        }
        private static Configuration.Config Configuration => SqlServerSnapshotSpecConfig.Create(SqlServerDbUtils.ConnectionString,"snapshotSpec");

        public SqlServerSnapshotSpec(ITestOutputHelper outputHelper, SqlServerFixture fixture) 
            : base(Initialize(fixture))
        {
            DebuggingHelpers.SetupTraceDump(outputHelper);
            var connFactory = new AkkaPersistenceDataConnectionFactory(
                new SnapshotConfig(
                    Configuration
                        .WithFallback(Linq2DbPersistence.DefaultConfiguration)
                        .GetConfig("akka.persistence.snapshot-store.linq2db")));
            
            using (var conn = connFactory.GetConnection())
            {
                try
                {
                    conn.GetTable<SnapshotRow>().Delete();
                }
                catch 
                {
                    // no-op
                }
            }
            
            Initialize();
        }
    }
}