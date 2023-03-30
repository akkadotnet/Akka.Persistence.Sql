// -----------------------------------------------------------------------
//  <copyright file="JournalTableConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;

namespace Akka.Persistence.Sql.Config
{
    public class JournalTableConfig : IEquatable<JournalTableConfig>
    {
        public JournalTableConfig(Configuration.Config config)
        {
            var mappingPath = config.GetString("table-mapping");
            if (string.IsNullOrEmpty(mappingPath))
                throw new ConfigurationException(
                    "The configuration property akka.persistence.journal.sql.table-mapping is null or empty");

            // backward compatibility
            var compatibility = config.GetString("table-compatibility-mode");
            if (compatibility != null)
                mappingPath = compatibility;

            var mappingConfig = config.GetConfig(mappingPath);
            if (mappingConfig is null)
                throw new ConfigurationException(
                    $"The configuration path akka.persistence.journal.sql.{mappingPath} does not exist");

            if (mappingPath != "default")
                mappingConfig = mappingConfig.WithFallback(SqlPersistence.DefaultJournalMappingConfiguration);

            SchemaName = mappingConfig.GetString("schema-name");

            EventJournalTable = new EventJournalTableConfig(mappingConfig);
            MetadataTable = new MetadataTableConfig(mappingConfig);
            TagTable = new TagTableConfig(mappingConfig);
        }

        public string SchemaName { get; }

        [Obsolete("Not implemented")]
        public bool UseEventManifestColumn { get; } = false;

        public EventJournalTableConfig EventJournalTable { get; }

        public MetadataTableConfig MetadataTable { get; }

        public TagTableConfig TagTable { get; }

        public bool Equals(JournalTableConfig other)
        {
            if (ReferenceEquals(null, other))
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return Equals(EventJournalTable, other.EventJournalTable) &&
                   Equals(MetadataTable, other.MetadataTable) &&
                   SchemaName == other.SchemaName;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;

            if (ReferenceEquals(this, obj))
                return true;

            return obj is JournalTableConfig j && Equals(j);
        }

        public override int GetHashCode()
            => HashCode.Combine(EventJournalTable, SchemaName, MetadataTable);
    }
}
