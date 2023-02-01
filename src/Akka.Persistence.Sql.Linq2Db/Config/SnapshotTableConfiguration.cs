using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Snapshot;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    
    public class SnapshotTableConfiguration
    {
        public SnapshotTableConfiguration(Configuration.Config config)
        {
            var mappingPath = config.GetString("table-mapping");
            if (string.IsNullOrEmpty(mappingPath))
                throw new ConfigurationException("The configuration property akka.persistence.journal.linq2db.table-mapping is null or empty");
            
            var mappingConfig = config.GetConfig(mappingPath);
            if (mappingConfig is null)
                throw new ConfigurationException($"The configuration path akka.persistence.journal.linq2db.{mappingPath} does not exist");
            
            if (mappingPath != "default")
                mappingConfig.WithFallback(config.GetConfig("default"));
            
            SchemaName = mappingConfig.GetString("schema-name");

            SnapshotTable = new SnapshotTableConfig(mappingConfig);
        }
        
        public SnapshotTableConfig SnapshotTable { get; }

        public string SchemaName { get; }
        
        public override int GetHashCode()
        {
            return HashCode.Combine(SnapshotTable, SchemaName);
        }
    }
}