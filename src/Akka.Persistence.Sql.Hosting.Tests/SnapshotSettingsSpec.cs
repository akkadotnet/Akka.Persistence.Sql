// -----------------------------------------------------------------------
//  <copyright file="SnapshotSettingsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Data;
using Akka.Configuration;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Extensions;
using FluentAssertions;
using Xunit;

namespace Akka.Persistence.Sql.Hosting.Tests
{
    public class SnapshotSettingsSpec
    {
        [Fact(DisplayName = "Default options should not override default hocon config")]
        public void DefaultOptionsTest()
        {
            var defaultConfig = ConfigurationFactory.ParseString(
                    @"
akka.persistence.snapshot-store.sql {
    connection-string = a
    provider-name = b
}")
                .WithFallback(SqlPersistence.DefaultConfiguration);

            defaultConfig = defaultConfig.GetConfig(SqlPersistence.SnapshotStoreConfigPath);

            var opt = new SqlSnapshotOptions
            {
                ConnectionString = "a",
                ProviderName = "b",
            };
            var actualConfig = opt.ToConfig().WithFallback(SqlPersistence.DefaultConfiguration);

            actualConfig = actualConfig.GetConfig(SqlPersistence.SnapshotStoreConfigPath);

            actualConfig.GetString("connection-string").Should().Be(defaultConfig.GetString("connection-string"));
            actualConfig.GetString("plugin-id").Should().Be(defaultConfig.GetString("plugin-id"));
            actualConfig.GetString("provider-name").Should().Be(defaultConfig.GetString("provider-name"));
            actualConfig.GetString("table-mapping").Should().Be(defaultConfig.GetString("table-mapping"));
            actualConfig.GetString("serializer").Should().Be(defaultConfig.GetString("serializer"));
            actualConfig.GetBoolean("auto-initialize").Should().Be(defaultConfig.GetBoolean("auto-initialize"));
            actualConfig.GetIsolationLevel("read-isolation-level").Should().Be(defaultConfig.GetIsolationLevel("read-isolation-level"));
            actualConfig.GetIsolationLevel("write-isolation-level").Should().Be(defaultConfig.GetIsolationLevel("write-isolation-level"));
            actualConfig.GetString("default.schema-name").Should().Be(defaultConfig.GetString("default.schema-name"));

            var defaultSnapshotConfig = defaultConfig.GetConfig("default.snapshot");
            var actualSnapshotConfig = actualConfig.GetConfig("default.snapshot");

            actualSnapshotConfig.GetString("table-name").Should().Be(defaultSnapshotConfig.GetString("table-name"));
            actualSnapshotConfig.GetString("columns.persistence-id").Should().Be(defaultSnapshotConfig.GetString("columns.persistence-id"));
            actualSnapshotConfig.GetString("columns.sequence-number").Should().Be(defaultSnapshotConfig.GetString("columns.sequence-number"));
            actualSnapshotConfig.GetString("columns.created").Should().Be(defaultSnapshotConfig.GetString("columns.created"));
            actualSnapshotConfig.GetString("columns.snapshot").Should().Be(defaultSnapshotConfig.GetString("columns.snapshot"));
            actualSnapshotConfig.GetString("columns.manifest").Should().Be(defaultSnapshotConfig.GetString("columns.manifest"));
            actualSnapshotConfig.GetString("columns.serializerId").Should().Be(defaultSnapshotConfig.GetString("columns.serializerId"));
        }

        [Fact(DisplayName = "Custom Options should modify default config")]
        public void ModifiedOptionsTest()
        {
            var opt = new SqlSnapshotOptions(false, "custom")
            {
                AutoInitialize = false,
                ConnectionString = "a",
                ProviderName = "b",
                Serializer = "hyperion",
                ReadIsolationLevel = IsolationLevel.Snapshot,
                WriteIsolationLevel = IsolationLevel.Snapshot,
                DatabaseOptions = new SnapshotDatabaseOptions(DatabaseMapping.SqlServer)
                {
                    SchemaName = "schema",
                    SnapshotTable = new SnapshotTableOptions
                    {
                        TableName = "aa",
                        PersistenceIdColumnName = "a",
                        SequenceNumberColumnName = "b",
                        CreatedColumnName = "c",
                        SnapshotColumnName = "d",
                        ManifestColumnName = "e",
                        SerializerIdColumnName = "f",
                    },
                },
            };

            var fullConfig = opt.ToConfig();
            var snapshotConfig = fullConfig
                .GetConfig("akka.persistence.snapshot-store.custom")
                .WithFallback(SqlPersistence.DefaultSnapshotConfiguration);
            var config = new SnapshotConfig(snapshotConfig);

            config.PluginId.Should().Be("akka.persistence.snapshot-store.custom");
            config.AutoInitialize.Should().BeFalse();
            config.ConnectionString.Should().Be("a");
            config.ProviderName.Should().Be("b");
            config.DefaultSerializer.Should().Be("hyperion");
            config.ReadIsolationLevel.Should().Be(IsolationLevel.Snapshot);
            config.WriteIsolationLevel.Should().Be(IsolationLevel.Snapshot);

            config.TableConfig.SchemaName.Should().Be("schema");

            var snapshotTable = config.TableConfig.SnapshotTable;
            snapshotTable.Name.Should().Be("aa");
            snapshotTable.ColumnNames.PersistenceId.Should().Be("a");
            snapshotTable.ColumnNames.SequenceNumber.Should().Be("b");
            snapshotTable.ColumnNames.Created.Should().Be("c");
            snapshotTable.ColumnNames.Snapshot.Should().Be("d");
            snapshotTable.ColumnNames.Manifest.Should().Be("e");
            snapshotTable.ColumnNames.SerializerId.Should().Be("f");
        }
    }
}
