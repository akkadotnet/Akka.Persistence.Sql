// -----------------------------------------------------------------------
//  <copyright file="JournalSettingsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Data;
using Akka.Configuration;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Extensions;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Journal.Dao;
using Akka.Persistence.Sql.Query;
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using Xunit;

namespace Akka.Persistence.Sql.Hosting.Tests
{
    public class JournalSettingsSpec
    {
        [Fact(DisplayName = "Default options should not override default hocon config")]
        public void DefaultOptionsTest()
        {
            #region Setup
            
            var expectedConfig = ConfigurationFactory.ParseString(
                    """
                    akka.persistence.journal.sql {
                        connection-string = a
                        provider-name = b
                    }
                    akka.persistence.query.journal.sql {
                        connection-string = a
                        provider-name = b
                    }
                    """)
                .WithFallback(SqlPersistence.DefaultConfiguration)
                .WithFallback(SqlPersistence.DefaultQueryConfiguration);

            var opt = new SqlJournalOptions
            {
                ConnectionString = "a",
                ProviderName = "b",
            };
            var config = opt.ToConfig()
                .WithFallback(SqlPersistence.DefaultConfiguration)
                .WithFallback(SqlPersistence.DefaultQueryConfiguration);
            
            #endregion

            #region Journal configuration
            
            var defaultConfig = expectedConfig.GetConfig(SqlPersistence.JournalConfigPath);
            var actualConfig = config.GetConfig(SqlPersistence.JournalConfigPath);

            actualConfig.AssertType(defaultConfig, "class", typeof(SqlWriteJournal));
            actualConfig.AssertString(defaultConfig, "plugin-id");
            actualConfig.AssertString(defaultConfig, "plugin-dispatcher");
            actualConfig.AssertString(defaultConfig, "connection-string", "a");
            actualConfig.AssertString(defaultConfig, "provider-name", "b");
            actualConfig.AssertBool(defaultConfig, "delete-compatibility-mode", false);
            actualConfig.AssertString(defaultConfig, "table-mapping", "default");
            actualConfig.AssertInt(defaultConfig, "buffer-size", 5000);
            actualConfig.AssertInt(defaultConfig, "batch-size", 100);
            actualConfig.AssertInt(defaultConfig, "db-round-trip-max-batch-size", 1000);
            actualConfig.AssertBool(defaultConfig, "prefer-parameters-on-multirow-insert", false);
            actualConfig.AssertInt(defaultConfig, "replay-batch-size", 1000);
            actualConfig.AssertInt(defaultConfig, "parallelism", 3);
            actualConfig.AssertInt(defaultConfig, "max-row-by-row-size", 100);
            actualConfig.AssertBool(defaultConfig, "use-clone-connection", true);
            actualConfig.AssertString(defaultConfig, "materializer-dispatcher");
            actualConfig.AssertString(defaultConfig, "tag-write-mode", "TagTable");
            actualConfig.AssertString(defaultConfig, "tag-separator", ";");
            actualConfig.AssertBool(defaultConfig, "auto-initialize", true);
            actualConfig.AssertBool(defaultConfig, "warn-on-auto-init-fail", true);
            actualConfig.AssertType(defaultConfig, "dao", typeof(ByteArrayJournalDao));
            actualConfig.AssertString(defaultConfig, "serializer");
            actualConfig.AssertIsolationLevel(defaultConfig, "read-isolation-level");
            actualConfig.AssertIsolationLevel(defaultConfig, "write-isolation-level");

            actualConfig.AssertMappingEquals(defaultConfig, "default");
            #endregion

            #region Query configuration

            var defaultQueryConfig = expectedConfig.GetConfig(SqlPersistence.QueryConfigPath);
            var actualQueryConfig = config.GetConfig(SqlPersistence.QueryConfigPath);

            actualQueryConfig.AssertType(defaultQueryConfig, "class", typeof(SqlReadJournalProvider));
            actualConfig.AssertString(defaultConfig, "plugin-id");
            actualQueryConfig.AssertString(defaultQueryConfig, "write-plugin", "akka.persistence.journal.sql");
            actualQueryConfig.AssertInt(defaultQueryConfig, "max-buffer-size", 500);
            actualQueryConfig.AssertTimeSpan(defaultQueryConfig, "refresh-interval", 1.Seconds());
            actualQueryConfig.AssertString(defaultQueryConfig, "connection-string", "a");
            actualQueryConfig.AssertString(defaultQueryConfig, "tag-read-mode", "TagTable");
            
            actualQueryConfig.AssertInt(defaultQueryConfig, "journal-sequence-retrieval.batch-size", 10000);
            actualQueryConfig.AssertInt(defaultQueryConfig, "journal-sequence-retrieval.max-tries", 10);
            actualQueryConfig.AssertTimeSpan(defaultQueryConfig, "journal-sequence-retrieval.query-delay", 1.Seconds());
            actualQueryConfig.AssertTimeSpan(defaultQueryConfig, "journal-sequence-retrieval.max-backoff-query-delay", 60.Seconds());
            actualQueryConfig.AssertTimeSpan(defaultQueryConfig, "journal-sequence-retrieval.ask-timeout", 1.Seconds());
            
            actualQueryConfig.AssertString(defaultQueryConfig, "provider-name", "b");
            actualQueryConfig.AssertString(defaultQueryConfig, "table-mapping", "default");
            actualQueryConfig.AssertInt(defaultQueryConfig, "buffer-size", 5000);
            actualQueryConfig.AssertInt(defaultQueryConfig, "batch-size", 100);
            actualQueryConfig.AssertInt(defaultQueryConfig, "replay-batch-size", 1000);
            actualQueryConfig.AssertInt(defaultQueryConfig, "parallelism", 3);
            actualQueryConfig.AssertInt(defaultQueryConfig, "max-row-by-row-size", 100);
            actualQueryConfig.AssertBool(defaultQueryConfig, "use-clone-connection", true);
            actualQueryConfig.AssertString(defaultQueryConfig, "tag-separator", ";");
            actualQueryConfig.AssertIsolationLevel(defaultQueryConfig, "read-isolation-level");
            actualQueryConfig.AssertType(defaultQueryConfig, "dao", typeof(ByteArrayJournalDao));
            
            actualQueryConfig.AssertMappingEquals(defaultQueryConfig, "default");
            
            #endregion
        }

        [Fact(DisplayName = "Custom Options should modify default config")]
        public void ModifiedOptionsTest()
        {
            var opt = new SqlJournalOptions(false, "custom")
            {
                AutoInitialize = false,
                ConnectionString = "a",
                ProviderName = "b",
                QueryRefreshInterval = 5.Seconds(),
                Serializer = "hyperion",
                ReadIsolationLevel = IsolationLevel.Snapshot,
                WriteIsolationLevel = IsolationLevel.Snapshot,
                TagStorageMode = TagMode.Csv,
                TagSeparator = ":",
                DeleteCompatibilityMode = true,
                DatabaseOptions = new JournalDatabaseOptions(DatabaseMapping.SqlServer)
                {
                    SchemaName = "schema",
                    JournalTable = new JournalTableOptions
                    {
                        TableName = "aa",
                        UseWriterUuidColumn = false,
                        OrderingColumnName = "a",
                        DeletedColumnName = "b",
                        PersistenceIdColumnName = "c",
                        SequenceNumberColumnName = "d",
                        CreatedColumnName = "e",
                        TagsColumnName = "f",
                        MessageColumnName = "g",
                        IdentifierColumnName = "h",
                        ManifestColumnName = "i",
                        WriterUuidColumnName = "j",
                    },
                    MetadataTable = new MetadataTableOptions
                    {
                        TableName = "bb",
                        PersistenceIdColumnName = "a",
                        SequenceNumberColumnName = "b",
                    },
                    TagTable = new TagTableOptions
                    {
                        TableName = "cc",
                        OrderingColumnName = "a",
                        TagColumnName = "b",
                        PersistenceIdColumnName = "c",
                        SequenceNumberColumnName = "d",
                    },
                },
            };

            var fullConfig = opt.ToConfig();

            #region Journal configuration
            var journalConfig = new JournalConfig(
                fullConfig
                    .GetConfig("akka.persistence.journal.custom")
                    .WithFallback(SqlPersistence.DefaultJournalConfiguration));

            journalConfig.PluginId.Should().Be("akka.persistence.journal.custom");
            journalConfig.AutoInitialize.Should().BeFalse();
            journalConfig.ConnectionString.Should().Be("a");
            journalConfig.ProviderName.Should().Be("b");
            journalConfig.DefaultSerializer.Should().Be("hyperion");
            journalConfig.UseCloneConnection.Should().BeTrue(); // non-overridable
            journalConfig.ReadIsolationLevel.Should().Be(IsolationLevel.Snapshot);
            journalConfig.WriteIsolationLevel.Should().Be(IsolationLevel.Snapshot);

            journalConfig.PluginConfig.TagSeparator.Should().Be(":");
            journalConfig.PluginConfig.TagMode.Should().Be(TagMode.Csv);

            journalConfig.DaoConfig.SqlCommonCompatibilityMode.Should().BeTrue();

            journalConfig.TableConfig.SchemaName.Should().Be("schema");

            var journalTable = journalConfig.TableConfig.EventJournalTable;
            journalTable.UseWriterUuidColumn.Should().BeFalse();
            journalTable.Name.Should().Be("aa");
            journalTable.ColumnNames.Ordering.Should().Be("a");
            journalTable.ColumnNames.Deleted.Should().Be("b");
            journalTable.ColumnNames.PersistenceId.Should().Be("c");
            journalTable.ColumnNames.SequenceNumber.Should().Be("d");
            journalTable.ColumnNames.Created.Should().Be("e");
            journalTable.ColumnNames.Tags.Should().Be("f");
            journalTable.ColumnNames.Message.Should().Be("g");
            journalTable.ColumnNames.Identifier.Should().Be("h");
            journalTable.ColumnNames.Manifest.Should().Be("i");
            journalTable.ColumnNames.WriterUuid.Should().Be("j");

            var metaTable = journalConfig.TableConfig.MetadataTable;
            metaTable.Name.Should().Be("bb");
            metaTable.ColumnNames.PersistenceId.Should().Be("a");
            metaTable.ColumnNames.SequenceNumber.Should().Be("b");

            var tagTable = journalConfig.TableConfig.TagTable;
            tagTable.Name.Should().Be("cc");
            tagTable.ColumnNames.OrderingId.Should().Be("a");
            tagTable.ColumnNames.Tag.Should().Be("b");
            tagTable.ColumnNames.PersistenceId.Should().Be("c");
            tagTable.ColumnNames.SequenceNumber.Should().Be("d");
            #endregion

            #region Query configuration
            var queryConfig = new ReadJournalConfig(
                fullConfig
                    .GetConfig("akka.persistence.query.journal.custom")
                    .WithFallback(SqlPersistence.DefaultQueryConfiguration));

            queryConfig.PluginId.Should().Be("akka.persistence.query.journal.custom");
            queryConfig.ConnectionString.Should().Be("a");
            queryConfig.ProviderName.Should().Be("b");
            queryConfig.WritePluginId.Should().Be("akka.persistence.journal.custom");
            queryConfig.DefaultSerializer.Should().Be("hyperion");
            queryConfig.UseCloneConnection.Should().BeTrue(); // non-overridable
            queryConfig.RefreshInterval.Should().Be(5.Seconds());

            queryConfig.PluginConfig.TagSeparator.Should().Be(":");
            queryConfig.PluginConfig.TagMode.Should().Be(TagMode.Csv);

            queryConfig.JournalSequenceRetrievalConfiguration.BatchSize.Should().Be(10000); // non-overridable
            queryConfig.JournalSequenceRetrievalConfiguration.MaxTries.Should().Be(10); // non-overridable
            queryConfig.JournalSequenceRetrievalConfiguration.QueryDelay.Should().Be(1.Seconds()); // non-overridable
            queryConfig.JournalSequenceRetrievalConfiguration.MaxBackoffQueryDelay.Should().Be(60.Seconds()); // non-overridable
            queryConfig.JournalSequenceRetrievalConfiguration.AskTimeout.Should().Be(1.Seconds()); // non-overridable
            
            queryConfig.TableConfig.SchemaName.Should().Be("schema");

            journalTable = queryConfig.TableConfig.EventJournalTable;
            journalTable.UseWriterUuidColumn.Should().BeFalse();
            journalTable.Name.Should().Be("aa");
            journalTable.ColumnNames.Ordering.Should().Be("a");
            journalTable.ColumnNames.Deleted.Should().Be("b");
            journalTable.ColumnNames.PersistenceId.Should().Be("c");
            journalTable.ColumnNames.SequenceNumber.Should().Be("d");
            journalTable.ColumnNames.Created.Should().Be("e");
            journalTable.ColumnNames.Tags.Should().Be("f");
            journalTable.ColumnNames.Message.Should().Be("g");
            journalTable.ColumnNames.Identifier.Should().Be("h");
            journalTable.ColumnNames.Manifest.Should().Be("i");
            journalTable.ColumnNames.WriterUuid.Should().Be("j");

            metaTable = queryConfig.TableConfig.MetadataTable;
            metaTable.Name.Should().Be("bb");
            metaTable.ColumnNames.PersistenceId.Should().Be("a");
            metaTable.ColumnNames.SequenceNumber.Should().Be("b");

            tagTable = queryConfig.TableConfig.TagTable;
            tagTable.Name.Should().Be("cc");
            tagTable.ColumnNames.OrderingId.Should().Be("a");
            tagTable.ColumnNames.Tag.Should().Be("b");
            tagTable.ColumnNames.PersistenceId.Should().Be("c");
            tagTable.ColumnNames.SequenceNumber.Should().Be("d");
            #endregion
        }
    }
}
