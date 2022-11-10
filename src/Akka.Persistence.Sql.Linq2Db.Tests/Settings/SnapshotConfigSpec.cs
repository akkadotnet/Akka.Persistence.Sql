// -----------------------------------------------------------------------
//  <copyright file="SnapshotConfigSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2009-2022 Lightbend Inc. <http://www.lightbend.com>
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Journal.Dao;
using Akka.Persistence.Sql.Linq2Db.Snapshot;
using FluentAssertions;
using Xunit;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Settings
{
    public class SnapshotConfigSpec
    {
        private readonly Configuration.Config _defaultConfig;
        
        public SnapshotConfigSpec()
        {
            _defaultConfig = Linq2DbPersistence.DefaultConfiguration();
        }

        [Fact(DisplayName = "Default snapshot HOCON config should contain default values")]
        public void DefaultJournalHoconConfigTest()
        {
            var snapshot = _defaultConfig.GetConfig("akka.persistence.snapshot-store.linq2db");
            snapshot.Should().NotBeNull();
            
            var stringType = snapshot.GetString("class");
            var type = Type.GetType(stringType);
            type.Should().Be(typeof(Linq2DbSnapshotStore));

            snapshot.GetString("plugin-dispatcher").Should()
                .Be("akka.persistence.dispatchers.default-plugin-dispatcher");
            snapshot.GetString("connection-string", "invalid").Should().BeNullOrEmpty();
            snapshot.GetString("provider-name", "invalid").Should().BeNullOrEmpty();
            snapshot.GetBoolean("use-clone-connection").Should().BeFalse();
            snapshot.GetString("table-compatibility-mode", "invalid").Should().BeNullOrEmpty();
            snapshot.GetString("serializer", "invalid").Should().BeNullOrEmpty();
            snapshot.GetString("dao", "invalid").Should()
                .Be("Akka.Persistence.Sql.Linq2Db.Journal.DAO.ByteArrayJournalDao, Akka.Persistence.Sql.Linq2Db");
            
            var snapshotTable = snapshot.GetConfig("tables.snapshot");
            snapshotTable.Should().NotBeNull();
            snapshotTable.GetBoolean("auto-init").Should().BeTrue();
            snapshotTable.GetBoolean("warn-on-auto-init-fail").Should().BeTrue();
            snapshotTable.GetString("table-name", "invalid").Should().Be("snapshot");
            snapshotTable.GetString("schema-name", "invalid").Should().BeNullOrEmpty();
        }
        
        [Fact(DisplayName = "Default snapshot config should contain default values")]
        public void DefaultSnapshotConfigTest()
        {
            var snapshotHocon = _defaultConfig.GetConfig("akka.persistence.snapshot-store.linq2db");
            snapshotHocon.Should().NotBeNull();

            var snapshot = new SnapshotConfig(snapshotHocon);
            // assert default values
            AssertDefaultSnapshotConfig(snapshot);
            
            var tableConfig = snapshot.TableConfig;

            // assert default snapshot column names
            var snapshotColumns = tableConfig.ColumnNames;
            snapshotColumns.PersistenceId.Should().Be("persistence_id");
            snapshotColumns.SequenceNumber.Should().Be("sequence_number");
            snapshotColumns.Created.Should().Be("created");
            snapshotColumns.Snapshot.Should().Be("snapshot");
            snapshotColumns.Manifest.Should().Be("manifest");
            snapshotColumns.SerializerId.Should().Be("serializer_id");
        }

        [Fact(DisplayName = "Snapshot config with SqlServer compat should contain correct SqlServer column names")]
        public void SqlServerSnapshotConfigTest()
        {
            var snapshotHocon = ConfigurationFactory
                .ParseString("akka.persistence.snapshot-store.linq2db.table-compatibility-mode = sqlserver")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.snapshot-store.linq2db");
            snapshotHocon.Should().NotBeNull();

            var snapshot = new SnapshotConfig(snapshotHocon);
            // assert default values
            AssertDefaultSnapshotConfig(snapshot);
            
            var tableConfig = snapshot.TableConfig;

            // assert default snapshot column names
            var snapshotColumns = tableConfig.ColumnNames;
            snapshotColumns.PersistenceId.Should().Be("PersistenceId");
            snapshotColumns.SequenceNumber.Should().Be("SequenceNr");
            snapshotColumns.Created.Should().Be("Timestamp");
            snapshotColumns.Snapshot.Should().Be("Snapshot");
            snapshotColumns.Manifest.Should().Be("Manifest");
            snapshotColumns.SerializerId.Should().Be("SerializerId");
        }

        [Fact(DisplayName = "Snapshot config with Sqlite compat should contain correct SqlServer column names")]
        public void SqliteSnapshotConfigTest()
        {
            var snapshotHocon = ConfigurationFactory
                .ParseString("akka.persistence.snapshot-store.linq2db.table-compatibility-mode = sqlite")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.snapshot-store.linq2db");
            snapshotHocon.Should().NotBeNull();

            var snapshot = new SnapshotConfig(snapshotHocon);
            // assert default values
            AssertDefaultSnapshotConfig(snapshot);
            
            var tableConfig = snapshot.TableConfig;

            // assert default snapshot column names
            var snapshotColumns = tableConfig.ColumnNames;
            snapshotColumns.PersistenceId.Should().Be("persistence_id");
            snapshotColumns.SequenceNumber.Should().Be("sequence_nr");
            snapshotColumns.Created.Should().Be("created_at");
            snapshotColumns.Snapshot.Should().Be("payload");
            snapshotColumns.Manifest.Should().Be("manifest");
            snapshotColumns.SerializerId.Should().Be("serializer_id");
        }

        [Fact(DisplayName = "Snapshot config with PostgreSql compat should contain correct SqlServer column names")]
        public void PostgreSqlSnapshotConfigTest()
        {
            var snapshotHocon = ConfigurationFactory
                .ParseString("akka.persistence.snapshot-store.linq2db.table-compatibility-mode = postgres")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.snapshot-store.linq2db");
            snapshotHocon.Should().NotBeNull();

            var snapshot = new SnapshotConfig(snapshotHocon);
            // assert default values
            AssertDefaultSnapshotConfig(snapshot);
            
            var tableConfig = snapshot.TableConfig;

            // assert default snapshot column names
            var snapshotColumns = tableConfig.ColumnNames;
            snapshotColumns.PersistenceId.Should().Be("persistence_id");
            snapshotColumns.SequenceNumber.Should().Be("sequence_nr");
            snapshotColumns.Created.Should().Be("created_at");
            snapshotColumns.Snapshot.Should().Be("payload");
            snapshotColumns.Manifest.Should().Be("manifest");
            snapshotColumns.SerializerId.Should().Be("serializer_id");
        }

        [Fact(DisplayName = "Snapshot config with MySql compat should contain correct SqlServer column names")]
        public void MySqlSnapshotConfigTest()
        {
            var snapshotHocon = ConfigurationFactory
                .ParseString("akka.persistence.snapshot-store.linq2db.table-compatibility-mode = mysql")
                .WithFallback(_defaultConfig)
                .GetConfig("akka.persistence.snapshot-store.linq2db");
            snapshotHocon.Should().NotBeNull();

            var snapshot = new SnapshotConfig(snapshotHocon);
            // assert default values
            AssertDefaultSnapshotConfig(snapshot);
            
            var tableConfig = snapshot.TableConfig;

            // assert default snapshot column names
            var snapshotColumns = tableConfig.ColumnNames;
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
            daoType.Should().Be(typeof(ByteArrayJournalDao));

            var daoConfig = snapshot.IDaoConfig;
            daoConfig.SqlCommonCompatibilityMode.Should().BeFalse();
            daoConfig.Parallelism.Should().Be(0); // this is not set
            
            var tableConfig = snapshot.TableConfig;
            tableConfig.AutoInitialize.Should().BeTrue();
            tableConfig.TableName.Should().Be("snapshot");
            tableConfig.SchemaName.Should().BeNullOrEmpty();
            tableConfig.WarnOnAutoInitializeFail.Should().BeTrue();
        }
    }
}