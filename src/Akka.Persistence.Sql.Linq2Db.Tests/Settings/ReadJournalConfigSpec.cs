// -----------------------------------------------------------------------
//  <copyright file="ReadJournalConfigSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Journal.Dao;
using Akka.Persistence.Sql.Linq2Db.Query;
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using Xunit;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Settings
{
    public class ReadJournalConfigSpec
    {
        private readonly Configuration.Config _defaultConfig;
        
        public ReadJournalConfigSpec()
        {
            _defaultConfig = Linq2DbPersistence.DefaultConfiguration();
        }

        [Fact(DisplayName = "Default journal query HOCON config should contain default values")]
        public void DefaultJournalQueryHoconConfigTest()
        {
            var query = _defaultConfig.GetConfig("akka.persistence.query.journal.linq2db");
            query.Should().NotBeNull();
            
            var stringType = query.GetString("class");
            var type = Type.GetType(stringType);
            type.Should().Be(typeof(Linq2DbReadJournalProvider));

            query.GetString("write-plugin", "invalid").Should().BeNullOrEmpty();
            query.GetInt("max-buffer-size").Should().Be(500);
            query.GetTimeSpan("refresh-interval").Should().Be(1.Seconds());
            query.GetString("connection-string", "invalid").Should().BeNullOrEmpty();
            query.GetString("provider-name", "invalid").Should().BeNullOrEmpty();
            query.GetBoolean("delete-compatibility-mode").Should().BeTrue();
            query.GetString("table-compatibility-mode", "invalid").Should().BeNullOrEmpty();
            query.GetInt("buffer-size").Should().Be(5000);
            query.GetInt("batch-size").Should().Be(100);
            query.GetInt("replay-batch-size").Should().Be(1000);
            query.GetInt("parallelism").Should().Be(3);
            query.GetInt("max-row-by-row-size").Should().Be(100);
            query.GetBoolean("use-clone-connection").Should().BeFalse();
            query.GetString("tag-separator", "invalid").Should().Be(";");
            query.GetString("dao", "invalid").Should()
                .Be("Akka.Persistence.Sql.Linq2Db.Journal.Dao.ByteArrayJournalDao, Akka.Persistence.Sql.Linq2Db");

            var retrieval = query.GetConfig("journal-sequence-retrieval");
            retrieval.Should().NotBeNull();
            retrieval.GetInt("batch-size").Should().Be(10000);
            retrieval.GetInt("max-tries").Should().Be(10);
            retrieval.GetTimeSpan("query-delay").Should().Be(1.Seconds());
            retrieval.GetTimeSpan("max-backoff-query-delay").Should().Be(1.Minutes());
            retrieval.GetTimeSpan("ask-timeout").Should().Be(1.Seconds());
            
            var journalTable = query.GetConfig("tables.journal");
            journalTable.Should().NotBeNull();
            journalTable.GetBoolean("auto-init").Should().BeTrue();
            journalTable.GetBoolean("warn-on-auto-init-fail").Should().BeTrue();
            journalTable.GetString("table-name", "invalid").Should().Be("journal");
            journalTable.GetString("metadata-table-name", "invalid").Should().Be("journal_metadata");
            journalTable.GetString("schema-name", "invalid").Should().BeNullOrEmpty();
        }
        
        [Fact(DisplayName = "Default journal query config should contain default values")]
        public void DefaultReadJournalConfigTest()
        {
            var journalHocon = _defaultConfig.GetConfig("akka.persistence.query.journal.linq2db");
            journalHocon.Should().NotBeNull();

            var journal = new ReadJournalConfig(journalHocon);
            // assert default values
            AssertDefaultQueryConfig(journal);
            
            var tableConfig = journal.TableConfig;

            // assert default journal column names
            var journalColumns = tableConfig.ColumnNames;
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
            var metaColumns = tableConfig.MetadataColumnNames;
            metaColumns.PersistenceId.Should().Be("persistenceId");
            metaColumns.SequenceNumber.Should().Be("sequenceNr");
        }

        [Fact(DisplayName = "Journal config with SqlServer compat should contain correct SqlServer column names")]
        public void SqlServerJournalConfigTest()
        {
            var journalHocon = ConfigurationFactory
                .ParseString("akka.persistence.query.journal.linq2db.table-compatibility-mode = sqlserver")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.query.journal.linq2db");
            journalHocon.Should().NotBeNull();
            
            var journal = new ReadJournalConfig(journalHocon);
            // assert default values
            AssertDefaultQueryConfig(journal);
            
            var tableConfig = journal.TableConfig;

            // assert default journal column names
            var journalColumns = tableConfig.ColumnNames;
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
            var metaColumns = tableConfig.MetadataColumnNames;
            metaColumns.PersistenceId.Should().Be("persistenceId");
            metaColumns.SequenceNumber.Should().Be("sequenceNr");
        }

        [Fact(DisplayName = "Journal config with Sqlite compat should contain correct SqlServer column names")]
        public void SqliteJournalConfigTest()
        {
            var journalHocon = ConfigurationFactory
                .ParseString("akka.persistence.query.journal.linq2db.table-compatibility-mode = sqlite")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.query.journal.linq2db");
            journalHocon.Should().NotBeNull();
            
            var journal = new ReadJournalConfig(journalHocon);
            // assert default values
            AssertDefaultQueryConfig(journal);
            
            var tableConfig = journal.TableConfig;

            // assert default journal column names
            var journalColumns = tableConfig.ColumnNames;
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
            var metaColumns = tableConfig.MetadataColumnNames;
            metaColumns.PersistenceId.Should().Be("persistence_id");
            metaColumns.SequenceNumber.Should().Be("sequence_nr");
        }

        [Fact(DisplayName = "Journal config with PostgreSql compat should contain correct SqlServer column names")]
        public void PostgreSqlJournalConfigTest()
        {
            var journalHocon = ConfigurationFactory
                .ParseString("akka.persistence.query.journal.linq2db.table-compatibility-mode = postgres")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.query.journal.linq2db");
            journalHocon.Should().NotBeNull();
            
            var journal = new ReadJournalConfig(journalHocon);
            // assert default values
            AssertDefaultQueryConfig(journal);
            
            var tableConfig = journal.TableConfig;

            // assert default journal column names
            var journalColumns = tableConfig.ColumnNames;
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
            var metaColumns = tableConfig.MetadataColumnNames;
            metaColumns.PersistenceId.Should().Be("persistence_id");
            metaColumns.SequenceNumber.Should().Be("sequence_nr");
        }

        [Fact(DisplayName = "Journal config with MySql compat should contain correct SqlServer column names")]
        public void MySqlJournalConfigTest()
        {
            var journalHocon = ConfigurationFactory
                .ParseString("akka.persistence.query.journal.linq2db.table-compatibility-mode = mysql")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.query.journal.linq2db");
            journalHocon.Should().NotBeNull();
            
            var journal = new ReadJournalConfig(journalHocon);
            // assert default values
            AssertDefaultQueryConfig(journal);
            
            var tableConfig = journal.TableConfig;

            // assert default journal column names
            var journalColumns = tableConfig.ColumnNames;
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
            var metaColumns = tableConfig.MetadataColumnNames;
            metaColumns.PersistenceId.Should().Be("persistence_id");
            metaColumns.SequenceNumber.Should().Be("sequence_nr");
        }

        private static void AssertDefaultQueryConfig(ReadJournalConfig journal)
        {
            journal.ConnectionString.Should().BeNullOrEmpty();
            journal.ProviderName.Should().BeNullOrEmpty();
            journal.UseCloneConnection.Should().BeFalse();
            journal.RefreshInterval.Should().Be(1.Seconds());
            journal.MaxBufferSize.Should().Be(500);
            journal.AddShutdownHook.Should().BeTrue();

            var pluginConfig = journal.PluginConfig;
            pluginConfig.TagSeparator.Should().Be(";");
            var daoType = Type.GetType(pluginConfig.Dao);
            daoType.Should().Be(typeof(ByteArrayJournalDao));

            var daoConfig = journal.DaoConfig;
            daoConfig.BufferSize.Should().Be(5000);
            daoConfig.BatchSize.Should().Be(100);
            // daoConfig.DbRoundTripBatchSize.Should().Be(1000); // Not used in query config
            // daoConfig.PreferParametersOnMultiRowInsert.Should().BeTrue(); // Not used in query config
            daoConfig.ReplayBatchSize.Should().Be(1000);
            daoConfig.Parallelism.Should().Be(3);
            daoConfig.MaxRowByRowSize.Should().Be(100);
            daoConfig.SqlCommonCompatibilityMode.Should().BeTrue();
            
            var tableConfig = journal.TableConfig;
            tableConfig.AutoInitialize.Should().BeTrue();
            tableConfig.TableName.Should().Be("journal");
            tableConfig.MetadataTableName.Should().Be("journal_metadata");
            tableConfig.SchemaName.Should().BeNullOrEmpty();
            tableConfig.WarnOnAutoInitializeFail.Should().BeTrue();
        }
    }
}