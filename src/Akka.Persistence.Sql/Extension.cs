// -----------------------------------------------------------------------
//  <copyright file="Extension.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Snapshot;

namespace Akka.Persistence.Sql
{
    public sealed class SqlPersistence : IExtension
    {
        public const string JournalConfigPath = "akka.persistence.journal.sql";
        public const string SnapshotStoreConfigPath = "akka.persistence.snapshot-store.sql";
        public const string QueryConfigPath = "akka.persistence.query.journal.sql";

        public static readonly Configuration.Config DefaultJournalConfiguration;
        public static readonly Configuration.Config DefaultSnapshotConfiguration;
        public static readonly Configuration.Config DefaultQueryConfiguration;
        public static readonly Configuration.Config DefaultConfiguration;
        public static readonly Configuration.Config DefaultJournalMappingConfiguration;
        public static readonly Configuration.Config DefaultSnapshotMappingConfiguration;
        public static readonly Configuration.Config DefaultQueryMappingConfiguration;

        public readonly Configuration.Config DefaultConfig = DefaultConfiguration;
        public readonly Configuration.Config DefaultJournalConfig = DefaultJournalConfiguration;
        public readonly Configuration.Config DefaultJournalMappingConfig = DefaultJournalMappingConfiguration;
        public readonly Configuration.Config DefaultSnapshotConfig = DefaultSnapshotConfiguration;
        public readonly Configuration.Config DefaultSnapshotMappingConfig = DefaultSnapshotMappingConfiguration;
        public readonly Configuration.Config DefaultQueryConfig = DefaultQueryConfiguration;

        static SqlPersistence()
        {
            var journalConfig = ConfigurationFactory.FromResource<SqlWriteJournal>("Akka.Persistence.Sql.persistence.conf");
            var snapshotConfig = ConfigurationFactory.FromResource<SqlSnapshotStore>("Akka.Persistence.Sql.snapshot.conf");

            DefaultConfiguration = journalConfig.WithFallback(snapshotConfig);

            DefaultJournalConfiguration = DefaultConfiguration.GetConfig(JournalConfigPath);
            DefaultSnapshotConfiguration = DefaultConfiguration.GetConfig(SnapshotStoreConfigPath);
            DefaultQueryConfiguration = DefaultConfiguration.GetConfig(QueryConfigPath);

            DefaultJournalMappingConfiguration = DefaultJournalConfiguration.GetConfig("default");
            DefaultSnapshotMappingConfiguration = DefaultSnapshotConfiguration.GetConfig("default");
            DefaultQueryMappingConfiguration = DefaultQueryConfiguration.GetConfig("default");
        }

        public SqlPersistence(ActorSystem system)
            => system.Settings.InjectTopLevelFallback(DefaultConfiguration);

        public static SqlPersistence Get(ActorSystem system)
            => system.WithExtension<SqlPersistence, SqlPersistenceProvider>();
    }

    /// <summary>
    ///     Singleton class used to setup Linq2Db for akka persistence plugin.
    /// </summary>
    public sealed class SqlPersistenceProvider : ExtensionIdProvider<SqlPersistence>
    {
        public override SqlPersistence CreateExtension(ExtendedActorSystem system) => new(system);
    }
}
