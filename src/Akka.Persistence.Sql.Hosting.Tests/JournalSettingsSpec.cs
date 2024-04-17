// -----------------------------------------------------------------------
//  <copyright file="JournalSettingsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Data;
using Akka.Configuration;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Extensions;
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
            var defaultConfig = ConfigurationFactory.ParseString(
                    @"
akka.persistence.journal.sql {
    connection-string = a
    provider-name = b
}")
                .WithFallback(SqlPersistence.DefaultConfiguration);

            defaultConfig = defaultConfig.GetConfig(SqlPersistence.JournalConfigPath);

            var opt = new SqlJournalOptions
            {
                ConnectionString = "a",
                ProviderName = "b",
            };
            var actualConfig = opt.ToConfig().WithFallback(SqlPersistence.DefaultConfiguration);

            actualConfig = actualConfig.GetConfig(SqlPersistence.JournalConfigPath);

            actualConfig.GetString("connection-string").Should().Be(defaultConfig.GetString("connection-string"));
            actualConfig.GetString("provider-name").Should().Be(defaultConfig.GetString("provider-name"));
            actualConfig.GetString("table-mapping").Should().Be(defaultConfig.GetString("table-mapping"));
            actualConfig.GetString("serializer").Should().Be(defaultConfig.GetString("serializer"));
            actualConfig.GetBoolean("auto-initialize").Should().Be(defaultConfig.GetBoolean("auto-initialize"));
            actualConfig.GetIsolationLevel("read-isolation-level").Should().Be(defaultConfig.GetIsolationLevel("read-isolation-level"));
            actualConfig.GetIsolationLevel("write-isolation-level").Should().Be(defaultConfig.GetIsolationLevel("write-isolation-level"));
            actualConfig.GetString("default.schema-name").Should().Be(defaultConfig.GetString("default.schema-name"));

            var defaultJournalConfig = defaultConfig.GetConfig("default.journal");
            var actualJournalConfig = actualConfig.GetConfig("default.journal");

            actualJournalConfig.GetBoolean("use-writer-uuid-column").Should().Be(defaultJournalConfig.GetBoolean("use-writer-uuid-column"));
            actualJournalConfig.GetString("table-name").Should().Be(defaultJournalConfig.GetString("table-name"));
            actualJournalConfig.GetString("columns.ordering").Should().Be(defaultJournalConfig.GetString("columns.ordering"));
            actualJournalConfig.GetString("columns.deleted").Should().Be(defaultJournalConfig.GetString("columns.deleted"));
            actualJournalConfig.GetString("columns.persistence-id").Should().Be(defaultJournalConfig.GetString("columns.persistence-id"));
            actualJournalConfig.GetString("columns.sequence-number").Should().Be(defaultJournalConfig.GetString("columns.sequence-number"));
            actualJournalConfig.GetString("columns.created").Should().Be(defaultJournalConfig.GetString("columns.created"));
            actualJournalConfig.GetString("columns.tags").Should().Be(defaultJournalConfig.GetString("columns.tags"));
            actualJournalConfig.GetString("columns.message").Should().Be(defaultJournalConfig.GetString("columns.message"));
            actualJournalConfig.GetString("columns.identifier").Should().Be(defaultJournalConfig.GetString("columns.identifier"));
            actualJournalConfig.GetString("columns.manifest").Should().Be(defaultJournalConfig.GetString("columns.manifest"));
            actualJournalConfig.GetString("columns.writer-uuid").Should().Be(defaultJournalConfig.GetString("columns.writer-uuid"));

            var defaultMetaConfig = defaultConfig.GetConfig("default.metadata");
            var actualMetaConfig = actualConfig.GetConfig("default.metadata");

            actualMetaConfig.GetString("table-name").Should().Be(defaultMetaConfig.GetString("table-name"));
            actualMetaConfig.GetString("columns.persistence-id").Should().Be(defaultMetaConfig.GetString("columns.persistence-id"));
            actualMetaConfig.GetString("columns.sequence-number").Should().Be(defaultMetaConfig.GetString("columns.sequence-number"));

            var defaultTagConfig = defaultConfig.GetConfig("default.tag");
            var actualTagConfig = actualConfig.GetConfig("default.tag");

            actualTagConfig.GetString("table-name").Should().Be(defaultTagConfig.GetString("table-name"));
            actualTagConfig.GetString("columns.ordering-id").Should().Be(defaultTagConfig.GetString("columns.ordering-id"));
            actualTagConfig.GetString("columns.tag-value").Should().Be(defaultTagConfig.GetString("columns.tag-value"));
            actualTagConfig.GetString("columns.persistence-id").Should().Be(defaultTagConfig.GetString("columns.persistence-id"));
            actualTagConfig.GetString("columns.sequence-nr").Should().Be(defaultTagConfig.GetString("columns.sequence-nr"));
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
            
            var journalConfig = fullConfig
                .GetConfig("akka.persistence.journal.custom")
                .WithFallback(SqlPersistence.DefaultJournalConfiguration);
            var config = new JournalConfig(journalConfig);

            fullConfig.GetTimeSpan("akka.persistence.query.journal.custom.refresh-interval").Should().Be(5.Seconds());

            config.AutoInitialize.Should().BeFalse();
            config.ConnectionString.Should().Be("a");
            config.ProviderName.Should().Be("b");
            config.DefaultSerializer.Should().Be("hyperion");
            config.ReadIsolationLevel.Should().Be(IsolationLevel.Snapshot);
            config.WriteIsolationLevel.Should().Be(IsolationLevel.Snapshot);

            config.TableConfig.SchemaName.Should().Be("schema");

            var journalTable = config.TableConfig.EventJournalTable;
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

            var metaTable = config.TableConfig.MetadataTable;
            metaTable.Name.Should().Be("bb");
            metaTable.ColumnNames.PersistenceId.Should().Be("a");
            metaTable.ColumnNames.SequenceNumber.Should().Be("b");

            var tagTable = config.TableConfig.TagTable;
            tagTable.Name.Should().Be("cc");
            tagTable.ColumnNames.OrderingId.Should().Be("a");
            tagTable.ColumnNames.Tag.Should().Be("b");
            tagTable.ColumnNames.PersistenceId.Should().Be("c");
            tagTable.ColumnNames.SequenceNumber.Should().Be("d");
        }
    }
}
