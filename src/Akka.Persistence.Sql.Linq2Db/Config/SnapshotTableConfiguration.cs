using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Snapshot;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    
    public class SnapshotTableConfiguration
    {
        public SnapshotTableConfiguration(Configuration.Config config)
        {
            var localCfg = config.GetConfig("tables.snapshot")
                .SafeWithFallback(config).SafeWithFallback(Configuration.Config.Empty);
            
            ColumnNames= new SnapshotTableColumnNames(config);
            TableName = config.GetString("table-name", localCfg.GetString("table-name", "snapshot"));
            SchemaName = localCfg.GetString("schema-name", null);
            AutoInitialize = localCfg.GetBoolean("auto-init", false);
            WarnOnAutoInitializeFail = localCfg.GetBoolean("warn-on-auto-init-fail", true);
        }

        public bool WarnOnAutoInitializeFail { get; }
        
        public SnapshotTableColumnNames ColumnNames { get; }
        
        public string TableName { get; }
        
        public string SchemaName { get; }
        
        public bool AutoInitialize { get; }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(WarnOnAutoInitializeFail, ColumnNames, TableName, SchemaName, AutoInitialize);
        }
    }
}