// -----------------------------------------------------------------------
//  <copyright file="SnapshotConfigSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Snapshot;
using FluentAssertions;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Settings
{
    public class SnapshotConfigSpec
    {
        private readonly Configuration.Config _defaultConfig;

        public SnapshotConfigSpec()
            => _defaultConfig = SqlPersistence.DefaultConfiguration;

        [Fact(DisplayName = "Default snapshot HOCON config should contain default values")]
        public void DefaultJournalHoconConfigTest()
        {
            var snapshot = _defaultConfig.GetConfig(
                "akka.persistence.snapshot-store.sql");

            snapshot.Should().NotBeNull();

            var stringType = snapshot.GetString("class");
            var type = Type.GetType(stringType);
            type.Should().Be(typeof(SqlSnapshotStore));

            snapshot.GetString("plugin-dispatcher").Should().Be("akka.persistence.dispatchers.default-plugin-dispatcher");
            snapshot.GetString("connection-string", "invalid").Should().BeNullOrEmpty();
            snapshot.GetString("provider-name", "invalid").Should().BeNullOrEmpty();
            snapshot.GetBoolean("use-clone-connection").Should().BeFalse();
            snapshot.GetString("table-mapping", "invalid").Should().Be("default");
            snapshot.GetString("serializer", "invalid").Should().BeNullOrEmpty();
            snapshot.GetString("dao", "invalid").Should().Be("Akka.Persistence.Sql.Snapshot.ByteArraySnapshotDao, Akka.Persistence.Sql");
            snapshot.GetBoolean("auto-initialize").Should().BeFalse();
            snapshot.GetBoolean("warn-on-auto-init-fail").Should().BeTrue();

            var snapshotConfig = snapshot.GetConfig("default");
            snapshotConfig.Should().NotBeNull();
            snapshotConfig.GetString("schema-name", "invalid").Should().BeNullOrEmpty();

            var table = snapshotConfig.GetConfig("snapshot");
            table.GetString("table-name", "invalid").Should().Be("snapshot");
        }

        [Fact(DisplayName = "Default snapshot config should contain default values")]
        public void DefaultSnapshotConfigTest()
        {
            var snapshotHocon = _defaultConfig.GetConfig(
                "akka.persistence.snapshot-store.sql");

            snapshotHocon.Should().NotBeNull();

            var snapshot = new SnapshotConfig(snapshotHocon);

            // assert default values
            AssertDefaultSnapshotConfig(snapshot);

            var tableConfig = snapshot.TableConfig;
            tableConfig.SchemaName.Should().BeNullOrEmpty();
            tableConfig.SnapshotTable.Name.Should().Be("snapshot");

            // assert default snapshot column names
            var snapshotColumns = tableConfig.SnapshotTable.ColumnNames;
            snapshotColumns.PersistenceId.Should().Be("persistence_id");
            snapshotColumns.SequenceNumber.Should().Be("sequence_number");
            snapshotColumns.Created.Should().Be("created");
            snapshotColumns.Snapshot.Should().Be("snapshot");
            snapshotColumns.Manifest.Should().Be("manifest");
            snapshotColumns.SerializerId.Should().Be("serializer_id");
        }

        [Fact(DisplayName = "Snapshot config with SqlServer compatibility should contain correct column names")]
        public void SqlServerSnapshotConfigTest()
        {
            var snapshotHocon = ConfigurationFactory
                .ParseString("akka.persistence.snapshot-store.sql.table-mapping = sql-server")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.snapshot-store.sql");

            snapshotHocon.Should().NotBeNull();

            var snapshot = new SnapshotConfig(snapshotHocon);

            // assert default values
            AssertDefaultSnapshotConfig(snapshot);

            var tableConfig = snapshot.TableConfig;
            tableConfig.SchemaName.Should().Be("dbo");
            tableConfig.SnapshotTable.Name.Should().Be("SnapshotStore");

            // assert default snapshot column names
            var snapshotColumns = tableConfig.SnapshotTable.ColumnNames;
            snapshotColumns.PersistenceId.Should().Be("PersistenceId");
            snapshotColumns.SequenceNumber.Should().Be("SequenceNr");
            snapshotColumns.Created.Should().Be("Timestamp");
            snapshotColumns.Snapshot.Should().Be("Snapshot");
            snapshotColumns.Manifest.Should().Be("Manifest");
            snapshotColumns.SerializerId.Should().Be("SerializerId");
        }

        [Fact(DisplayName = "Snapshot config with Sqlite compatibility should contain correct column names")]
        public void SqliteSnapshotConfigTest()
        {
            var snapshotHocon = ConfigurationFactory
                .ParseString("akka.persistence.snapshot-store.sql.table-mapping = sqlite")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.snapshot-store.sql");

            snapshotHocon.Should().NotBeNull();

            var snapshot = new SnapshotConfig(snapshotHocon);

            // assert default values
            AssertDefaultSnapshotConfig(snapshot);

            var tableConfig = snapshot.TableConfig;
            tableConfig.SchemaName.Should().BeNullOrEmpty();
            tableConfig.SnapshotTable.Name.Should().Be("snapshot");

            // assert default snapshot column names
            var snapshotColumns = tableConfig.SnapshotTable.ColumnNames;
            snapshotColumns.PersistenceId.Should().Be("persistence_id");
            snapshotColumns.SequenceNumber.Should().Be("sequence_nr");
            snapshotColumns.Created.Should().Be("created_at");
            snapshotColumns.Snapshot.Should().Be("payload");
            snapshotColumns.Manifest.Should().Be("manifest");
            snapshotColumns.SerializerId.Should().Be("serializer_id");
        }

        [Fact(DisplayName = "Snapshot config with PostgreSql compatibility should contain correct column names")]
        public void PostgreSqlSnapshotConfigTest()
        {
            var snapshotHocon = ConfigurationFactory
                .ParseString("akka.persistence.snapshot-store.sql.table-mapping = postgresql")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.snapshot-store.sql");

            snapshotHocon.Should().NotBeNull();

            var snapshot = new SnapshotConfig(snapshotHocon);

            // assert default values
            AssertDefaultSnapshotConfig(snapshot);

            var tableConfig = snapshot.TableConfig;
            tableConfig.SchemaName.Should().Be("public");
            tableConfig.SnapshotTable.Name.Should().Be("snapshot_store");

            // assert default snapshot column names
            var snapshotColumns = tableConfig.SnapshotTable.ColumnNames;
            snapshotColumns.PersistenceId.Should().Be("persistence_id");
            snapshotColumns.SequenceNumber.Should().Be("sequence_nr");
            snapshotColumns.Created.Should().Be("created_at");
            snapshotColumns.Snapshot.Should().Be("payload");
            snapshotColumns.Manifest.Should().Be("manifest");
            snapshotColumns.SerializerId.Should().Be("serializer_id");
        }

        [Fact(DisplayName = "Snapshot config with MySql compatibility should contain correct column names")]
        public void MySqlSnapshotConfigTest()
        {
            var snapshotHocon = ConfigurationFactory
                .ParseString("akka.persistence.snapshot-store.sql.table-mapping = mysql")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.snapshot-store.sql");

            snapshotHocon.Should().NotBeNull();

            var snapshot = new SnapshotConfig(snapshotHocon);

            // assert default values
            AssertDefaultSnapshotConfig(snapshot);

            var tableConfig = snapshot.TableConfig;
            tableConfig.SchemaName.Should().BeNullOrEmpty();
            tableConfig.SnapshotTable.Name.Should().Be("snapshot_store");

            // assert default snapshot column names
            var snapshotColumns = tableConfig.SnapshotTable.ColumnNames;
            snapshotColumns.PersistenceId.Should().Be("persistence_id");
            snapshotColumns.SequenceNumber.Should().Be("sequence_nr");
            snapshotColumns.Created.Should().Be("created_at");
            snapshotColumns.Snapshot.Should().Be("snapshot");
            snapshotColumns.Manifest.Should().Be("manifest");
            snapshotColumns.SerializerId.Should().Be("serializer_id");
        }

        private static void AssertDefaultSnapshotConfig(SnapshotConfig snapshot)
        {
            snapshot.ConnectionString.Should().BeNullOrEmpty();
            snapshot.ProviderName.Should().BeNullOrEmpty();
            snapshot.UseCloneConnection.Should().BeFalse();
            snapshot.DefaultSerializer.Should().BeNullOrEmpty();
            snapshot.UseSharedDb.Should().BeNullOrEmpty();

            var pluginConfig = snapshot.PluginConfig;
            var daoType = Type.GetType(pluginConfig.Dao);
            daoType.Should().Be(typeof(ByteArraySnapshotDao));

            var daoConfig = snapshot.IDaoConfig;
            daoConfig.SqlCommonCompatibilityMode.Should().BeFalse();
            daoConfig.Parallelism.Should().Be(0); // this is not set
        }
    }
}
