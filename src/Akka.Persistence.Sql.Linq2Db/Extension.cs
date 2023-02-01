// -----------------------------------------------------------------------
//  <copyright file="Extension.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Snapshot;

namespace Akka.Persistence.Sql.Linq2Db
{
    public sealed class Linq2DbPersistence : IExtension
    {
        public const string JournalConfigPath = "akka.persistence.journal.linq2db";
        public const string SnapshotStoreConfigPath = "akka.persistence.snapshot-store.linq2db";
    
        /// <summary>
        /// Returns a default configuration for akka persistence Linq2Db-based journals and snapshot stores.
        /// </summary>
        /// <returns></returns>
        public static Configuration.Config DefaultConfiguration()
        {
            var journalConfig =  ConfigurationFactory.FromResource<Linq2DbWriteJournal>("Akka.Persistence.Sql.Linq2Db.persistence.conf");
            var snapshotConfig = ConfigurationFactory.FromResource<Linq2DbSnapshotStore>("Akka.Persistence.Sql.Linq2Db.snapshot.conf");
        
            return journalConfig.WithFallback(snapshotConfig);
        }    
    
        public readonly Configuration.Config DefaultJournalConfig;
        public readonly Configuration.Config DefaultSnapshotConfig;
        public readonly Configuration.Config DefaultConfig;
        public readonly Configuration.Config DefaultJournalMappingConfig;
        public readonly Configuration.Config DefaultSnapshotMappingConfig;

        public Linq2DbPersistence(ExtendedActorSystem system)
        {
            DefaultConfig = DefaultConfiguration();
            system.Settings.InjectTopLevelFallback(DefaultConfig);

            DefaultJournalConfig = DefaultConfig.GetConfig(JournalConfigPath);
            DefaultSnapshotConfig = DefaultConfig.GetConfig(SnapshotStoreConfigPath);

            DefaultJournalMappingConfig = DefaultJournalConfig.GetConfig("default");
            DefaultSnapshotMappingConfig = DefaultSnapshotConfig.GetConfig("default");
        }
    
        public static Linq2DbPersistence Get(ActorSystem system)
        {
            return system.WithExtension<Linq2DbPersistence, Linq2DbPersistenceProvider>();
        }
    }

    /// <summary>
    ///     Singleton class used to setup Linq2Db for akka persistence plugin.
    /// </summary>
    public sealed class Linq2DbPersistenceProvider : ExtensionIdProvider<Linq2DbPersistence>
    {
        public override Linq2DbPersistence CreateExtension(ExtendedActorSystem system)
        {
            return new Linq2DbPersistence(system);
        }
    }    
}
