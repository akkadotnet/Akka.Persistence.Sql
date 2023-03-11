// -----------------------------------------------------------------------
//  <copyright file="JournalConfigSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Journal.Dao;
using FluentAssertions;
using Xunit;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Settings
{
    public class JournalConfigSpec
    {
        private readonly Configuration.Config _defaultConfig;

        public JournalConfigSpec()
        {
            _defaultConfig = Linq2DbPersistence.DefaultConfiguration;
        }

        [Fact(DisplayName = "Default journal HOCON config should contain default values")]
        public void DefaultJournalHoconConfigTest()
        {
            var journal = _defaultConfig.GetConfig("akka.persistence.journal.linq2db");
            journal.Should().NotBeNull();

            var stringType = journal.GetString("class");
            var type = Type.GetType(stringType);
            type.Should().Be(typeof(Linq2DbWriteJournal));

            journal.GetString("plugin-dispatcher").Should()
                .Be("akka.persistence.dispatchers.default-plugin-dispatcher");
            journal.GetString("connection-string", "invalid").Should().BeNullOrEmpty();
            journal.GetString("provider-name", "invalid").Should().BeNullOrEmpty();
            journal.GetBoolean("delete-compatibility-mode").Should().BeTrue();
            journal.GetString("table-mapping", "invalid").Should().Be("default");
            journal.GetInt("buffer-size").Should().Be(5000);
            journal.GetInt("batch-size").Should().Be(100);
            journal.GetInt("db-round-trip-max-batch-size").Should().Be(1000);
            journal.GetBoolean("prefer-parameters-on-multirow-insert").Should().BeTrue();
            journal.GetInt("replay-batch-size").Should().Be(1000);
            journal.GetInt("parallelism").Should().Be(3);
            journal.GetInt("max-row-by-row-size").Should().Be(100);
            journal.GetBoolean("use-clone-connection").Should().BeFalse();
            journal.GetString("materializer-dispatcher", "invalid").Should().Be("akka.actor.default-dispatcher");
            journal.GetString("serializer", "invalid").Should().BeNullOrEmpty();
            journal.GetString("tag-separator", "invalid").Should().Be(";");
            journal.GetString("dao", "invalid").Should()
                .Be("Akka.Persistence.Sql.Linq2Db.Journal.Dao.ByteArrayJournalDao, Akka.Persistence.Sql.Linq2Db");
            journal.GetBoolean("auto-initialize").Should().BeFalse();
            journal.GetBoolean("warn-on-auto-init-fail").Should().BeTrue();

            var journalTables = journal.GetConfig("default");
            journalTables.Should().NotBeNull();
            journalTables.GetString("schema-name", "invalid").Should().BeNullOrEmpty();

            var journalTable = journalTables.GetConfig("journal");
            journalTable.GetString("table-name", "invalid").Should().Be("journal");

            var metadataTable = journalTables.GetConfig("metadata");
            metadataTable.GetString("table-name", "invalid").Should().Be("journal_metadata");
        }

        [Fact(DisplayName = "Default journal config should contain default values")]
        public void DefaultJournalConfigTest()
        {
            var journalHocon = _defaultConfig.GetConfig("akka.persistence.journal.linq2db");
            journalHocon.Should().NotBeNull();

            var journal = new JournalConfig(journalHocon);
            // assert default values
            AssertDefaultJournalConfig(journal);

            var tableConfig = journal.TableConfig;
            var journalTable = tableConfig.EventJournalTable;
            var metadataTable = tableConfig.MetadataTable;

            tableConfig.SchemaName.Should().BeNullOrEmpty();
            journalTable.Name.Should().Be("journal");
            metadataTable.Name.Should().Be("journal_metadata");

            // assert default journal column names
            var journalColumns = journalTable.ColumnNames;
            journalColumns.Ordering.Should().Be("ordering");
            journalColumns.Deleted.Should().Be("deleted");
            journalColumns.PersistenceId.Should().Be("persistence_id");
            journalColumns.SequenceNumber.Should().Be("sequence_number");
            journalColumns.Created.Should().Be("created");
            journalColumns.Tags.Should().Be("tags");
            journalColumns.Message.Should().Be("message");
            journalColumns.Identifier.Should().Be("identifier");
            journalColumns.Manifest.Should().Be("manifest");

            // assert default metadata column names
            var metaColumns = metadataTable.ColumnNames;
            metaColumns.PersistenceId.Should().Be("persistence_id");
            metaColumns.SequenceNumber.Should().Be("sequence_number");
        }

        [Fact(DisplayName = "Journal config with SqlServer compat should contain correct column names")]
        public void SqlServerJournalConfigTest()
        {
            var journalHocon = ConfigurationFactory
                .ParseString("akka.persistence.journal.linq2db.table-mapping = sql-server")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.journal.linq2db");
            journalHocon.Should().NotBeNull();

            var journal = new JournalConfig(journalHocon);
            // assert default values
            AssertDefaultJournalConfig(journal);

            var tableConfig = journal.TableConfig;
            var journalTable = tableConfig.EventJournalTable;
            var metadataTable = tableConfig.MetadataTable;

            tableConfig.SchemaName.Should().Be("dbo");
            journalTable.Name.Should().Be("EventJournal");
            metadataTable.Name.Should().Be("Metadata");

            // assert default journal column names
            var journalColumns = journalTable.ColumnNames;
            journalColumns.Ordering.Should().Be("Ordering");
            journalColumns.Deleted.Should().Be("IsDeleted");
            journalColumns.PersistenceId.Should().Be("PersistenceId");
            journalColumns.SequenceNumber.Should().Be("SequenceNr");
            journalColumns.Created.Should().Be("Timestamp");
            journalColumns.Tags.Should().Be("Tags");
            journalColumns.Message.Should().Be("Payload");
            journalColumns.Identifier.Should().Be("SerializerId");
            journalColumns.Manifest.Should().Be("Manifest");

            // assert default metadata column names
            var metaColumns = metadataTable.ColumnNames;
            metaColumns.PersistenceId.Should().Be("PersistenceId");
            metaColumns.SequenceNumber.Should().Be("SequenceNr");
        }

        [Fact(DisplayName = "Journal config with Sqlite compat should contain correct column names")]
        public void SqliteJournalConfigTest()
        {
            var journalHocon = ConfigurationFactory
                .ParseString("akka.persistence.journal.linq2db.table-mapping = sqlite")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.journal.linq2db");
            journalHocon.Should().NotBeNull();

            var journal = new JournalConfig(journalHocon);
            // assert default values
            AssertDefaultJournalConfig(journal);

            var tableConfig = journal.TableConfig;
            var journalTable = tableConfig.EventJournalTable;
            var metadataTable = tableConfig.MetadataTable;

            tableConfig.SchemaName.Should().BeNullOrEmpty();
            journalTable.Name.Should().Be("event_journal");
            metadataTable.Name.Should().Be("journal_metadata");

            // assert default journal column names
            var journalColumns = journalTable.ColumnNames;
            journalColumns.Ordering.Should().Be("ordering");
            journalColumns.Deleted.Should().Be("is_deleted");
            journalColumns.PersistenceId.Should().Be("persistence_id");
            journalColumns.SequenceNumber.Should().Be("sequence_nr");
            journalColumns.Created.Should().Be("timestamp");
            journalColumns.Tags.Should().Be("tags");
            journalColumns.Message.Should().Be("payload");
            journalColumns.Identifier.Should().Be("serializer_id");
            journalColumns.Manifest.Should().Be("manifest");

            // assert default metadata column names
            var metaColumns = metadataTable.ColumnNames;
            metaColumns.PersistenceId.Should().Be("persistence_id");
            metaColumns.SequenceNumber.Should().Be("sequence_nr");
        }

        [Fact(DisplayName = "Journal config with PostgreSql compat should contain correct column names")]
        public void PostgreSqlJournalConfigTest()
        {
            var journalHocon = ConfigurationFactory
                .ParseString("akka.persistence.journal.linq2db.table-mapping = postgresql")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.journal.linq2db");
            journalHocon.Should().NotBeNull();

            var journal = new JournalConfig(journalHocon);
            // assert default values
            AssertDefaultJournalConfig(journal);

            var tableConfig = journal.TableConfig;
            var journalTable = tableConfig.EventJournalTable;
            var metadataTable = tableConfig.MetadataTable;

            tableConfig.SchemaName.Should().Be("public");
            journalTable.Name.Should().Be("event_journal");
            metadataTable.Name.Should().Be("metadata");

            // assert default journal column names
            var journalColumns = journalTable.ColumnNames;
            journalColumns.Ordering.Should().Be("ordering");
            journalColumns.Deleted.Should().Be("is_deleted");
            journalColumns.PersistenceId.Should().Be("persistence_id");
            journalColumns.SequenceNumber.Should().Be("sequence_nr");
            journalColumns.Created.Should().Be("created_at");
            journalColumns.Tags.Should().Be("tags");
            journalColumns.Message.Should().Be("payload");
            journalColumns.Identifier.Should().Be("serializer_id");
            journalColumns.Manifest.Should().Be("manifest");

            // assert default metadata column names
            var metaColumns = metadataTable.ColumnNames;
            metaColumns.PersistenceId.Should().Be("persistence_id");
            metaColumns.SequenceNumber.Should().Be("sequence_nr");
        }

        [Fact(DisplayName = "Journal config with MySql compat should contain correct column names")]
        public void MySqlJournalConfigTest()
        {
            var journalHocon = ConfigurationFactory
                .ParseString("akka.persistence.journal.linq2db.table-mapping = mysql")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.journal.linq2db");
            journalHocon.Should().NotBeNull();

            var journal = new JournalConfig(journalHocon);
            // assert default values
            AssertDefaultJournalConfig(journal);

            var tableConfig = journal.TableConfig;
            var journalTable = tableConfig.EventJournalTable;
            var metadataTable = tableConfig.MetadataTable;

            tableConfig.SchemaName.Should().BeNullOrEmpty();
            journalTable.Name.Should().Be("event_journal");
            metadataTable.Name.Should().Be("metadata");

            // assert default journal column names
            var journalColumns = journalTable.ColumnNames;
            journalColumns.Ordering.Should().Be("ordering");
            journalColumns.Deleted.Should().Be("is_deleted");
            journalColumns.PersistenceId.Should().Be("persistence_id");
            journalColumns.SequenceNumber.Should().Be("sequence_nr");
            journalColumns.Created.Should().Be("created_at");
            journalColumns.Tags.Should().Be("tags");
            journalColumns.Message.Should().Be("payload");
            journalColumns.Identifier.Should().Be("serializer_id");
            journalColumns.Manifest.Should().Be("manifest");

            // assert default metadata column names
            var metaColumns = metadataTable.ColumnNames;
            metaColumns.PersistenceId.Should().Be("persistence_id");
            metaColumns.SequenceNumber.Should().Be("sequence_nr");
        }

        private static void AssertDefaultJournalConfig(JournalConfig journal)
        {
            journal.ConnectionString.Should().BeNullOrEmpty();
            journal.MaterializerDispatcher.Should().Be("akka.actor.default-dispatcher");
            journal.ProviderName.Should().BeNullOrEmpty();
            journal.UseSharedDb.Should().BeNullOrEmpty();
            journal.UseCloneConnection.Should().BeFalse();
            journal.DefaultSerializer.Should().BeNullOrEmpty();
            journal.AutoInitialize.Should().BeFalse();
            journal.WarnOnAutoInitializeFail.Should().BeTrue();

            var pluginConfig = journal.PluginConfig;
            pluginConfig.TagSeparator.Should().Be(";");
            var daoType = Type.GetType(pluginConfig.Dao);
            daoType.Should().Be(typeof(ByteArrayJournalDao));

            var daoConfig = journal.DaoConfig;
            daoConfig.BufferSize.Should().Be(5000);
            daoConfig.BatchSize.Should().Be(100);
            daoConfig.DbRoundTripBatchSize.Should().Be(1000);
            daoConfig.PreferParametersOnMultiRowInsert.Should().BeTrue();
            daoConfig.ReplayBatchSize.Should().Be(1000);
            daoConfig.Parallelism.Should().Be(3);
            daoConfig.MaxRowByRowSize.Should().Be(100);
            daoConfig.SqlCommonCompatibilityMode.Should().BeTrue();
        }
    }
}