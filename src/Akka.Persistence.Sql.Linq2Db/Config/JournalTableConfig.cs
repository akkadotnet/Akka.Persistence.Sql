﻿using System;
using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    [Flags]
    public enum TagWriteMode
    {
        CommaSeparatedArray = 1,
        TagTable = 2,
        CommaSeparatedArrayAndTagTable = 3,
    }
    
    public enum TagTableMode
    {
        OrderingId,
        SequentialUUID
    }
    
    public class JournalTableConfig
    {
        public JournalTableColumnNames ColumnNames { get; }
        public string TableName { get; }
        public string SchemaName { get; }
        public bool AutoInitialize { get; }
        public string MetadataTableName { get; }
        public MetadataTableColumnNames MetadataColumnNames { get; }
        public bool WarnOnAutoInitializeFail { get; }

        public TagWriteMode TagWriteMode { get; }
        public TagTableMode TagTableMode { get; }
        public string? TagTableName { get; }
        public bool UseEventManifestColumn { get; }
        
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
            
            var s = config.GetString("tag-write-mode", "default");
            if (Enum.TryParse<TagWriteMode>(s, true, out TagWriteMode res))
            {

            }
            else if (s.Equals("default", StringComparison.InvariantCultureIgnoreCase))
            {
                res = TagWriteMode.CommaSeparatedArray;
            }
            else if (s.Equals("migration",
                         StringComparison.InvariantCultureIgnoreCase))
            {
                res = TagWriteMode.CommaSeparatedArrayAndTagTable;
            }
            else if (s.Equals("tagtableonly",
                         StringComparison.InvariantCultureIgnoreCase))
            {
                res = TagWriteMode.TagTable;
            }
            else
            {
                res = TagWriteMode.CommaSeparatedArray;
            }
            TagWriteMode = res;
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
                Equals(MetadataColumnNames, other.MetadataColumnNames) &&
                TagWriteMode== other.TagWriteMode;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is JournalTableConfig j && Equals(j);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ColumnNames, TableName, SchemaName, AutoInitialize, MetadataTableName, MetadataColumnNames, TagWriteMode);
        }
    }
}