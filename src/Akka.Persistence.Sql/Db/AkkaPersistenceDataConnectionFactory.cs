// -----------------------------------------------------------------------
//  <copyright file="AkkaPersistenceDataConnectionFactory.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Snapshot;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Data.RetryPolicy;
using LinqToDB.DataProvider.SqlServer;
using LinqToDB.Mapping;

namespace Akka.Persistence.Sql.Db
{
    public class AkkaPersistenceDataConnectionFactory
    {
        private readonly Lazy<AkkaDataConnection> _cloneConnection;
        private readonly DataOptions _opts;
        private readonly IRetryPolicy? _policy;
        private readonly bool _useCloneDataConnection;

        public AkkaPersistenceDataConnectionFactory(IProviderConfig<JournalTableConfig> config)
        {
            // Build Mapping Schema to be used for all connections.
            // Make a unique mapping schema name here to avoid problems
            // with multiple configurations using different schemas.
            var configName = "akka.persistence.l2db." + HashCode.Combine(
                config.ConnectionString,
                config.ProviderName,
                config.TableConfig.GetHashCode());

            var mappingSchema = new MappingSchema(configName, MappingSchema.Default);

            var fmb = new FluentMappingBuilder(mappingSchema);
            MapJournalRow(config, fmb);
            MapMetadataRow(config, fmb);
            MapTagRow(config, fmb);
            fmb.Build();

            _useCloneDataConnection = config.UseCloneConnection;

            _opts = BuildDataOptions(config, mappingSchema);

            if (config.ProviderName.ToLower().StartsWith("sqlserver"))
                _policy = new SqlServerRetryPolicy();
            
            
            _cloneConnection = new Lazy<AkkaDataConnection>(
                () => new AkkaDataConnection(
                    config.ProviderName,
                    new DataConnection(_opts)));
        }

        public AkkaPersistenceDataConnectionFactory(IProviderConfig<SnapshotTableConfiguration> config)
        {
            // Build Mapping Schema to be used for all connections.
            // Make a unique mapping schema name here to avoid problems
            // with multiple configurations using different schemas.
            var configName = "akka.persistence.l2db." + HashCode.Combine(
                config.ConnectionString,
                config.ProviderName,
                config.TableConfig.GetHashCode());

            var mappingSchema = new MappingSchema(configName, MappingSchema.Default);

            var fmb = new FluentMappingBuilder(mappingSchema);

            if (config.ProviderName.ToLower().Contains("sqlserver"))
            {
                MapDateTimeSnapshotRow(config, fmb);
            }
            else
            {
                MapLongSnapshotRow(config, fmb);
            }

            fmb.Build();

            _useCloneDataConnection = config.UseCloneConnection;

            _opts = BuildDataOptions(config, mappingSchema);

            if (config.ProviderName.ToLower().StartsWith("sqlserver"))
                _policy = new SqlServerRetryPolicy();

            _cloneConnection = new Lazy<AkkaDataConnection>(
                () => new AkkaDataConnection(
                    config.ProviderName,
                    new DataConnection(_opts)));
        }

        private static void MapJournalRow(
            IProviderConfig<JournalTableConfig> config,
            FluentMappingBuilder fmb)
        {
            var tableConfig = config.TableConfig;
            var journalConfig = tableConfig.EventJournalTable;
            var columnNames = journalConfig.ColumnNames;
            var rowBuilder = fmb.Entity<JournalRow>();

            if (tableConfig.SchemaName is not null)
                rowBuilder.HasSchemaName(tableConfig.SchemaName);

            rowBuilder
                .HasTableName(journalConfig.Name)

                .Member(r => r.Ordering)
                .HasColumnName(columnNames.Ordering)
                .IsIdentity()
                .IsPrimaryKey()

                .Member(r => r.Deleted)
                .HasColumnName(columnNames.Deleted)

                .Member(r => r.Manifest)
                .HasColumnName(columnNames.Manifest)
                .HasLength(500)

                .Member(r => r.Message)
                .HasColumnName(columnNames.Message)
                .IsNullable(false)

                .Member(r => r.Identifier)
                .HasColumnName(columnNames.Identifier)

                .Member(r => r.PersistenceId)
                .HasColumnName(columnNames.PersistenceId)
                .HasLength(255)
                .IsNullable(false)

                .Member(r => r.SequenceNumber)
                .HasColumnName(columnNames.SequenceNumber)

                .Member(r => r.Timestamp)
                .HasColumnName(columnNames.Created)

                .Member(r => r.TagArray)
                .IsNotColumn();

            if (config.ProviderName.StartsWith(ProviderName.MySql))
            {
                rowBuilder
                    .Member(r => r.Message)
                    .HasDbType("LONGBLOB");
            }

            if (config.ProviderName.ToLower().Contains("sqlite"))
            {
                rowBuilder
                    .Member(r => r.Ordering)
                    .HasDbType("INTEGER");
            }

            if (journalConfig.UseWriterUuidColumn)
            {
                rowBuilder
                    .Member(r => r.WriterUuid)
                    .HasColumnName(columnNames.WriterUuid)
                    .HasLength(128);
            }
            else
            {
                // non-default legacy tables does not have WriterUuid column defined.
                rowBuilder
                    .Member(r => r.WriterUuid)
                    .IsNotColumn();
            }

            // We can skip writing tags the old way by ignoring the column in mapping.
            if (config.PluginConfig.TagMode == TagMode.TagTable)
            {
                rowBuilder
                    .Member(r => r.Tags)
                    .IsNotColumn();
            }
            else
            {
                rowBuilder
                    .Member(r => r.Tags)
                    .HasColumnName(columnNames.Tags)
                    .HasLength(100);
            }

            // TODO: UseEventManifestColumn is always false
            if (config.TableConfig.UseEventManifestColumn)
            {
                rowBuilder
                    .Member(r => r.EventManifest)
                    .IsColumn()
                    .HasLength(64);
            }
            else
            {
                rowBuilder
                    .Member(r => r.EventManifest)
                    .IsNotColumn();
            }
        }

        private static void MapMetadataRow(
            IProviderConfig<JournalTableConfig> config,
            FluentMappingBuilder fmb)
        {
            if (!config.IDaoConfig.SqlCommonCompatibilityMode)
                return;

            // Probably overkill, but we only set Metadata Mapping if specified
            // That we are in delete compatibility mode.
            var tableConfig = config.TableConfig;
            var rowBuilder = fmb.Entity<JournalMetaData>();

            if (tableConfig.SchemaName is not null)
                rowBuilder.HasSchemaName(tableConfig.SchemaName);

            rowBuilder
                .HasTableName(tableConfig.MetadataTable.Name)

                .Member(r => r.PersistenceId)
                .HasColumnName(tableConfig.MetadataTable.ColumnNames.PersistenceId)
                .HasLength(255)
                .IsPrimaryKey()

                .Member(r => r.SequenceNumber)
                .HasColumnName(tableConfig.MetadataTable.ColumnNames.SequenceNumber)
                .IsPrimaryKey();
        }

        private static void MapTagRow(
            IProviderConfig<JournalTableConfig> config,
            FluentMappingBuilder fmb)
        {
            if (config.PluginConfig.TagMode is TagMode.Csv)
                return;

            var tableConfig = config.TableConfig;
            var tagConfig = tableConfig.TagTable;
            var columnNames = tagConfig.ColumnNames;
            var rowBuilder = fmb.Entity<JournalTagRow>();

            if (tableConfig.SchemaName is not null)
                rowBuilder.HasSchemaName(tableConfig.SchemaName);

            rowBuilder
                .HasTableName(tagConfig.Name)

                .Member(r => r.OrderingId)
                .HasColumnName(columnNames.OrderingId)
                .IsNullable(false)
                .IsPrimaryKey()

                .Member(r => r.TagValue)
                .HasColumnName(columnNames.Tag)
                .IsNullable(false)
                .HasLength(64)
                .IsPrimaryKey()

                .Member(r => r.PersistenceId)
                .HasColumnName(columnNames.PersistenceId)
                .HasLength(255)
                .IsNullable(false)

                .Member(r => r.SequenceNumber)
                .HasColumnName(columnNames.SequenceNumber)
                .IsNullable(false);

            if (config.ProviderName.ToLower().Contains("sqlite"))
            {
                rowBuilder
                    .Member(r => r.OrderingId)
                    .HasDbType("INTEGER")

                    .Member(r => r.SequenceNumber)
                    .HasDbType("INTEGER");
            }
        }

        private static void MapDateTimeSnapshotRow(
            IProviderConfig<SnapshotTableConfiguration> config,
            FluentMappingBuilder fmb)
        {
            var tableConfig = config.TableConfig;
            var snapshotConfig = tableConfig.SnapshotTable;
            var rowBuilder = fmb.Entity<DateTimeSnapshotRow>();

            if (tableConfig.SchemaName is not null)
                rowBuilder.HasSchemaName(tableConfig.SchemaName);

            rowBuilder
                .HasTableName(snapshotConfig.Name)

                .Member(r => r.PersistenceId)
                .HasColumnName(snapshotConfig.ColumnNames.PersistenceId)
                .HasLength(255)
                .IsPrimaryKey()

                .Member(r => r.SequenceNumber)
                .HasColumnName(snapshotConfig.ColumnNames.SequenceNumber)
                .IsPrimaryKey()

                .Member(r => r.Created)
                .HasColumnName(snapshotConfig.ColumnNames.Created)

                .Member(r => r.Manifest)
                .HasColumnName(snapshotConfig.ColumnNames.Manifest)
                .HasLength(500)

                .Member(r => r.Payload)
                .HasColumnName(snapshotConfig.ColumnNames.Snapshot)

                .Member(r => r.SerializerId)
                .HasColumnName(snapshotConfig.ColumnNames.SerializerId);

            if (config.ProviderName.StartsWith(ProviderName.MySql))
            {
                rowBuilder
                    .Member(r => r.Payload)
                    .HasDbType("LONGBLOB");
            }

            if (config.IDaoConfig.SqlCommonCompatibilityMode)
            {
                //builder.Member(r => r.Created)
                //    .HasConversion(l => DateTimeHelpers.FromUnixEpochMillis(l),
                //        dt => DateTimeHelpers.ToUnixEpochMillis(dt));
            }
        }

        private static void MapLongSnapshotRow(
            IProviderConfig<SnapshotTableConfiguration> config,
            FluentMappingBuilder fmb)
        {
            var tableConfig = config.TableConfig;
            var snapshotConfig = tableConfig.SnapshotTable;
            var rowBuilder = fmb.Entity<LongSnapshotRow>();

            if (tableConfig.SchemaName is not null)
                rowBuilder.HasSchemaName(tableConfig.SchemaName);

            rowBuilder
                .HasTableName(snapshotConfig.Name)

                .Member(r => r.PersistenceId)
                .HasColumnName(snapshotConfig.ColumnNames.PersistenceId)
                .HasLength(255)
                .IsPrimaryKey()

                .Member(r => r.SequenceNumber)
                .HasColumnName(snapshotConfig.ColumnNames.SequenceNumber)
                .IsPrimaryKey()

                .Member(r => r.Created)
                .HasColumnName(snapshotConfig.ColumnNames.Created)

                .Member(r => r.Manifest)
                .HasColumnName(snapshotConfig.ColumnNames.Manifest)
                .HasLength(500)

                .Member(r => r.Payload)
                .HasColumnName(snapshotConfig.ColumnNames.Snapshot)

                .Member(r => r.SerializerId)
                .HasColumnName(snapshotConfig.ColumnNames.SerializerId);

            if (config.ProviderName.StartsWith(ProviderName.MySql))
            {
                rowBuilder
                    .Member(r => r.Payload)
                    .HasDbType("LONGBLOB");
            }

            if (config.IDaoConfig.SqlCommonCompatibilityMode)
            {
                //builder.Member(r => r.Created)
                //    .HasConversion(l => DateTimeHelpers.FromUnixEpochMillis(l),
                //        dt => DateTimeHelpers.ToUnixEpochMillis(dt));
            }
        }

        private static DataOptions BuildDataOptions<TTable>(IProviderConfig<TTable> config, MappingSchema mappingSchema)
        {
            // LinqToDB.Data.DataConnection.ConfigurationApplier extracts different combinations therefore we can't
            // just override the connection string or the provider name. If data options are set, we assume that a valid
            // connection can be created.
            var options = config.DataOptions ?? new DataOptions().UseConnectionString(config.ProviderName, config.ConnectionString);
            return options.UseMappingSchema(mappingSchema);
        }

        public AkkaDataConnection GetConnection()
        {
            if (!_useCloneDataConnection)
                return new AkkaDataConnection(
                    _opts.ConnectionOptions.ProviderName!,
                    new DataConnection(_opts)
                    {
                        RetryPolicy = _policy,
                    });

            var connection = _cloneConnection.Value.Clone();
            connection.RetryPolicy = _policy;
            return connection;
        }
    }
}
