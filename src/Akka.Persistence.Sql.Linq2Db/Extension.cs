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

        public static readonly Configuration.Config DefaultJournalConfiguration;
        public static readonly Configuration.Config DefaultSnapshotConfiguration;
        public static readonly Configuration.Config DefaultConfiguration;
        public static readonly Configuration.Config DefaultJournalMappingConfiguration;
        public static readonly Configuration.Config DefaultSnapshotMappingConfiguration;

        public readonly Configuration.Config DefaultJournalConfig = DefaultJournalConfiguration;
        public readonly Configuration.Config DefaultSnapshotConfig = DefaultSnapshotConfiguration;
        public readonly Configuration.Config DefaultConfig = DefaultConfiguration;
        public readonly Configuration.Config DefaultJournalMappingConfig = DefaultJournalMappingConfiguration;
        public readonly Configuration.Config DefaultSnapshotMappingConfig = DefaultSnapshotMappingConfiguration;

        static Linq2DbPersistence()
        {
            var journalConfig =  ConfigurationFactory.FromResource<Linq2DbWriteJournal>("Akka.Persistence.Sql.Linq2Db.persistence.conf");
            var snapshotConfig = ConfigurationFactory.FromResource<Linq2DbSnapshotStore>("Akka.Persistence.Sql.Linq2Db.snapshot.conf");

            DefaultConfiguration = journalConfig.WithFallback(snapshotConfig);

            DefaultJournalConfiguration = DefaultConfiguration.GetConfig(JournalConfigPath);
            DefaultSnapshotConfiguration = DefaultConfiguration.GetConfig(SnapshotStoreConfigPath);

            DefaultJournalMappingConfiguration = DefaultJournalConfiguration.GetConfig("default");
            DefaultSnapshotMappingConfiguration = DefaultSnapshotConfiguration.GetConfig("default");
        }

        public Linq2DbPersistence(ExtendedActorSystem system)
        {
            system.Settings.InjectTopLevelFallback(DefaultConfig);
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
