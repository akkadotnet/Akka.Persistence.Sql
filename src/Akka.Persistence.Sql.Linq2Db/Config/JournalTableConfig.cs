using System;
using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class JournalTableConfig
    {
        public JournalTableColumnNames ColumnNames { get; }
        public string TableName { get; }
        public string SchemaName { get; }
        public bool AutoInitialize { get; }
        public string MetadataTableName { get; }
        public MetadataTableColumnNames MetadataColumnNames { get; }
        public bool WarnOnAutoInitializeFail { get; }
        public JournalTableConfig(Configuration.Config config)
        {
            var localCfg = config.GetConfig("tables.journal")
                .SafeWithFallback(config) //For easier compatibility with old cfgs.
                .SafeWithFallback(Configuration.Config.Empty);
            
            ColumnNames= new JournalTableColumnNames(config);
            MetadataColumnNames = new MetadataTableColumnNames(config);
            TableName = localCfg.GetString("table-name", "journal");
            MetadataTableName = localCfg.GetString("metadata-table-name", "journal_metadata");
            SchemaName = localCfg.GetString("schema-name", null);
            AutoInitialize = localCfg.GetBoolean("auto-init", false);
            WarnOnAutoInitializeFail = localCfg.GetBoolean("warn-on-auto-init-fail", true);
        }

        protected bool Equals(JournalTableConfig other)
        {
            return 
                Equals(ColumnNames, other.ColumnNames) &&
                TableName == other.TableName &&
                SchemaName == other.SchemaName &&
                AutoInitialize == other.AutoInitialize &&
                MetadataTableName == other.MetadataTableName &&
                WarnOnAutoInitializeFail == other.WarnOnAutoInitializeFail &&
                Equals(MetadataColumnNames, other.MetadataColumnNames);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is JournalTableConfig j && Equals(j);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ColumnNames, TableName, SchemaName, AutoInitialize, MetadataTableName, MetadataColumnNames);
        }
    }
}