// //-----------------------------------------------------------------------
// // <copyright file="SQLiteCompatibilitySpecConfig.cs" company="Akka.NET Project">
// //     Copyright (C) 2009-2020 Lightbend Inc. <http://www.lightbend.com>
// //     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// // </copyright>
// //-----------------------------------------------------------------------

using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Snapshot;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public class SqliteCompatibilitySpecConfig
    {
        public static Config InitSnapshotConfig(string tableName, string connectionString)
        {
            //need to make sure db is created before the tests start
            //DbUtils.Initialize(connString);
            var specString = $@"
akka.persistence {{
	publish-plugin-commands = on
	snapshot-store {{
		sqlite {{
			class = ""Akka.Persistence.Sqlite.Snapshot.SqliteSnapshotStore, Akka.Persistence.Sqlite""
			plugin-dispatcher = ""akka.actor.default-dispatcher""
			connection-string = ""{connectionString}""
			connection-timeout = 30s
			schema-name = dbo
			table-name = ""{tableName}""
			auto-initialize = on
		}}
	
		linq2db {{
			class = ""{typeof(Linq2DbSnapshotStore).AssemblyQualifiedName}""
            plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
			connection-string = ""{connectionString}""
			provider-name = ""{LinqToDB.ProviderName.SQLiteMS}""
			table-mapping = sqlite
            auto-initialize = true
            sqlite {{
                snapshot {{
                    table-name = ""{tableName}"" 
                }}
            }}
		}}
	}}
}}";

            return ConfigurationFactory.ParseString(specString)
	            .WithFallback(Linq2DbPersistence.DefaultConfiguration());
        }
        
        public static Config InitJournalConfig(string tableName, string metadataTableName, string connectionString)
        {
            var specString = $@"
akka.persistence {{
	publish-plugin-commands = on
	journal {{
		plugin = ""akka.persistence.journal.sqlite""
		sqlite {{
			class = ""Akka.Persistence.Sqlite.Journal.SqliteJournal, Akka.Persistence.Sqlite""
			plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
			table-name = ""{tableName}""
			metadata-table-name = ""{metadataTableName}""
			schema-name = dbo
			auto-initialize = on
			connection-string = ""{connectionString}""
		}}
		linq2db {{
			class = ""{typeof(Linq2DbWriteJournal).AssemblyQualifiedName}""
			plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
			connection-string = ""{connectionString}""
			provider-name = ""{LinqToDB.ProviderName.SQLiteMS}""
			parallelism = 3
            table-mapping = sqlite
            auto-initialize = true
            sqlite {{
                journal {{
                    table-name = ""{tableName}"" 
                }}
                metadata {{
                    table-name = ""{metadataTableName}"" 
                }}
			}}
		}}
	}}
}}";

            return ConfigurationFactory.ParseString(specString);
        }
    }
}