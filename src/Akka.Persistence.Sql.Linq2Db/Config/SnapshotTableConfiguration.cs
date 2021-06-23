using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Snapshot;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    
    public class SnapshotTableConfiguration
    {
        public SnapshotTableConfiguration(Configuration.Config config)
        {
            
            var localcfg = config.GetConfig("tables.snapshot")
                .SafeWithFallback(config).SafeWithFallback(Configuration.Config.Empty);
            ColumnNames= new SnapshotTableColumnNames(config);
            TableName = config.GetString("table-name", localcfg.GetString("table-name", "snapshot"));
            SchemaName = localcfg.GetString("schema-name", null);
            AutoInitialize = localcfg.GetBoolean("auto-init", false);
            WarnOnAutoInitializeFail =
                localcfg.GetBoolean("warn-on-auto-init-fail", true);
        }

        public bool WarnOnAutoInitializeFail { get; protected set; }
        public SnapshotTableColumnNames ColumnNames { get; protected set; }
        public string TableName { get; protected set; }
        public string SchemaName { get; protected set; }
        public bool AutoInitialize { get; protected set; }
        public override int GetHashCode()
        {
            return HashCode.Combine(ColumnNames, TableName, SchemaName);
        }
    }
}