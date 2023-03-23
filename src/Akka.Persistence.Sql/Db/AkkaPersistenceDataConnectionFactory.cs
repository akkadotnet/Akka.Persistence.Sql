// -----------------------------------------------------------------------
//  <copyright file="AkkaPersistenceDataConnectionFactory.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Snapshot;
using LinqToDB.Configuration;
using LinqToDB.Data;
using LinqToDB.Data.RetryPolicy;
using LinqToDB.DataProvider.SqlServer;
using LinqToDB.Mapping;

namespace Akka.Persistence.Sql.Db
{
    public class AkkaPersistenceDataConnectionFactory
    {
        private readonly Lazy<AkkaDataConnection> _cloneConnection;
        private readonly LinqToDBConnectionOptions _opts;
        private readonly IRetryPolicy _policy;
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

            var fmb = new MappingSchema(configName, MappingSchema.Default).GetFluentMappingBuilder();
            MapJournalRow(config, fmb);

            _useCloneDataConnection = config.UseCloneConnection;
            var mappingSchema = fmb.MappingSchema;
            _opts = new LinqToDBConnectionOptionsBuilder()
                .UseConnectionString(config.ProviderName, config.ConnectionString)
                .UseMappingSchema(mappingSchema)
                .Build();

            if (config.ProviderName.ToLower().StartsWith("sqlserver"))
                _policy = new SqlServerRetryPolicy();

            _cloneConnection = new Lazy<AkkaDataConnection>(
                () => new AkkaDataConnection(
                    _opts.ProviderName,
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

            var fmb = new MappingSchema(configName, MappingSchema.Default).GetFluentMappingBuilder();

            if (config.ProviderName.ToLower().Contains("sqlite") ||
                config.ProviderName.ToLower().Contains("postgresql"))
            {
                MapLongSnapshotRow(config, fmb);
            }
            else
            {
                MapDateTimeSnapshotRow(config, fmb);
            }

            _useCloneDataConnection = config.UseCloneConnection;
            var mappingSchema = fmb.MappingSchema;
            _opts = new LinqToDBConnectionOptionsBuilder()
                .UseConnectionString(config.ProviderName, config.ConnectionString)
                .UseMappingSchema(mappingSchema)
                .Build();

            if (config.ProviderName.ToLower().StartsWith("sqlserver"))
                _policy = new SqlServerRetryPolicy();

            _cloneConnection = new Lazy<AkkaDataConnection>(
                () => new AkkaDataConnection(
                    _opts.ProviderName,
                    new DataConnection(_opts)));
        }

        private static void MapJournalRow(
            IProviderConfig<JournalTableConfig> config,
            FluentMappingBuilder fmb)
        {
            var tableConfig = config.TableConfig;
            var journalConfig = tableConfig.EventJournalTable;
            var columnNames = journalConfig.ColumnNames;
            var journalRowBuilder = fmb.Entity<JournalRow>();

            if (tableConfig.SchemaName is { })
                journalRowBuilder.HasSchemaName(tableConfig.SchemaName);

            journalRowBuilder
                .HasTableName(journalConfig.Name)

                .Member(r => r.Deleted)
                .HasColumnName(columnNames.Deleted)

                .Member(r => r.Manifest)
                .HasColumnName(columnNames.Manifest)
                .HasLength(500)

                .Member(r => r.Message)
                .HasColumnName(columnNames.Message)
                .IsNullable(false)

                .Member(r => r.Ordering)
                .HasColumnName(columnNames.Ordering)

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

                // TODO: Disabling this for now, will need a migration script to support this
                .Member(r => r.WriteUuid)
                .IsNotColumn()

                .Member(r => r.TagArr)
                .IsNotColumn();

            // We can skip writing tags the old way by ignoring the column in mapping.
            if (tableConfig.TagWriteMode == TagWriteMode.TagTable)
            {
                journalRowBuilder
                    .Member(r => r.Tags)
                    .IsNotColumn();
            }
            else
            {
                journalRowBuilder
                    .Member(r => r.Tags)
                    .HasColumnName(columnNames.Tags)
                    .HasLength(100);
            }

            if (config.ProviderName.ToLower().Contains("sqlite"))
            {
                journalRowBuilder
                    .Member(r => r.Ordering)
                    .IsPrimaryKey()
                    .HasDbType("INTEGER")
                    .IsIdentity();
            }
            else
            {
                journalRowBuilder
                    .Member(r => r.Ordering).IsIdentity()
                    .Member(r => r.PersistenceId).IsPrimaryKey()
                    .Member(r => r.SequenceNumber).IsPrimaryKey();
            }

            if (config.TableConfig.UseEventManifestColumn)
            {
                journalRowBuilder
                    .Member(r => r.EventManifest)
                    .IsColumn()
                    .HasLength(64);
            }
            else
            {
                journalRowBuilder
                    .Member(r => r.EventManifest)
                    .IsNotColumn();
            }

            if (config.TableConfig.TagWriteMode is not TagWriteMode.Csv)
            {
                var tagConfig = tableConfig.TagTable;
                var tagColumns = tagConfig.ColumnNames;

                var rowBuilder = fmb.Entity<JournalTagRow>();
                if (tableConfig.SchemaName is { })
                    rowBuilder.HasSchemaName(tableConfig.SchemaName);

                rowBuilder
                    .HasTableName(tagConfig.Name)

                    .Member(r => r.TagValue)
                    .HasColumnName(tagColumns.Tag)
                    .IsColumn()
                    .IsNullable(false)
                    .HasLength(64)
                    .IsPrimaryKey()

                    .Member(r => r.SequenceNumber)
                    .HasColumnName(tagColumns.SequenceNumber)
                    .IsColumn()
                    .IsNullable(false)

                    .Member(r => r.PersistenceId)
                    .HasColumnName(tagColumns.PersistenceId)
                    .HasLength(255)
                    .IsColumn()
                    .IsNullable(false)
                    .IsPrimaryKey();

                if (config.ProviderName.ToLower().Contains("sqlite"))
                {
                    rowBuilder
                        .Member(r => r.OrderingId)
                        .HasColumnName(tagColumns.OrderingId)
                        .HasDbType("INTEGER")
                        .IsColumn()
                        .IsNullable(false)
                        .IsPrimaryKey();

                    rowBuilder
                        .Member(r => r.SequenceNumber)
                        .HasColumnName(tagColumns.SequenceNumber)
                        .HasDbType("INTEGER")
                        .IsColumn()
                        .IsNullable(false);
                }
                else
                {
                    rowBuilder
                        .Member(r => r.OrderingId)
                        .HasColumnName(tagColumns.OrderingId)
                        .IsColumn()
                        .IsNullable(false)
                        .IsPrimaryKey();

                    rowBuilder
                        .Member(r => r.SequenceNumber)
                        .HasColumnName(tagColumns.SequenceNumber)
                        .IsColumn()
                        .IsNullable(false);
                }
            }

            // Probably overkill, but we only set Metadata Mapping if specified
            // That we are in delete compatibility mode.
            if (config.IDaoConfig.SqlCommonCompatibilityMode)
            {
                var rowBuilder = fmb.Entity<JournalMetaData>();
                if (tableConfig.SchemaName is { })
                    rowBuilder.HasSchemaName(tableConfig.SchemaName);

                rowBuilder
                    .HasTableName(tableConfig.MetadataTable.Name)

                    .Member(r => r.PersistenceId)
                    .HasColumnName(tableConfig.MetadataTable.ColumnNames.PersistenceId)
                    .HasLength(255)

                    .Member(r => r.SequenceNumber)
                    .HasColumnName(tableConfig.MetadataTable.ColumnNames.SequenceNumber);
            }
        }

        private static void MapDateTimeSnapshotRow(
            IProviderConfig<SnapshotTableConfiguration> config,
            FluentMappingBuilder fmb)
        {
            var tableConfig = config.TableConfig;
            var snapshotConfig = tableConfig.SnapshotTable;
            var builder = fmb.Entity<DateTimeSnapshotRow>();

            if (tableConfig.SchemaName is { })
                builder.HasSchemaName(tableConfig.SchemaName);

            builder
                .HasTableName(snapshotConfig.Name)

                .Member(r => r.Created)
                .HasColumnName(snapshotConfig.ColumnNames.Created)

                .Member(r => r.Manifest)
                .HasColumnName(snapshotConfig.ColumnNames.Manifest)
                .HasLength(500)

                .Member(r => r.Payload)
                .HasColumnName(snapshotConfig.ColumnNames.Snapshot)

                .Member(r => r.SequenceNumber)
                .HasColumnName(snapshotConfig.ColumnNames.SequenceNumber)

                .Member(r => r.SerializerId)
                .HasColumnName(snapshotConfig.ColumnNames.SerializerId)

                .Member(r => r.PersistenceId)
                .HasColumnName(snapshotConfig.ColumnNames.PersistenceId)
                .HasLength(255);

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
            var builder = fmb.Entity<LongSnapshotRow>();

            if (tableConfig.SchemaName is { })
                builder.HasSchemaName(tableConfig.SchemaName);

            builder
                .HasTableName(snapshotConfig.Name)

                .Member(r => r.Created)
                .HasColumnName(snapshotConfig.ColumnNames.Created)

                .Member(r => r.Manifest)
                .HasColumnName(snapshotConfig.ColumnNames.Manifest)
                .HasLength(500)

                .Member(r => r.Payload)
                .HasColumnName(snapshotConfig.ColumnNames.Snapshot)

                .Member(r => r.SequenceNumber)
                .HasColumnName(snapshotConfig.ColumnNames.SequenceNumber)

                .Member(r => r.SerializerId)
                .HasColumnName(snapshotConfig.ColumnNames.SerializerId)

                .Member(r => r.PersistenceId)
                .HasColumnName(snapshotConfig.ColumnNames.PersistenceId)
                .HasLength(255);

            if (config.IDaoConfig.SqlCommonCompatibilityMode)
            {
                //builder.Member(r => r.Created)
                //    .HasConversion(l => DateTimeHelpers.FromUnixEpochMillis(l),
                //        dt => DateTimeHelpers.ToUnixEpochMillis(dt));
            }
        }

        public AkkaDataConnection GetConnection()
        {
            if (!_useCloneDataConnection)
                return new AkkaDataConnection(
                    _opts.ProviderName,
                    new DataConnection(_opts)
                    {
                        RetryPolicy = _policy
                    });

            var connection = _cloneConnection.Value.Clone();
            connection.RetryPolicy = _policy;
            return connection;
        }
    }
}
