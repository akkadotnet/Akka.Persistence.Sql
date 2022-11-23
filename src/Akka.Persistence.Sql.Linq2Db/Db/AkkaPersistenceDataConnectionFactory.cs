using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Snapshot;
using Akka.Util;
using LinqToDB;
using LinqToDB.Configuration;
using LinqToDB.Data;
using LinqToDB.Data.RetryPolicy;
using LinqToDB.DataProvider.SqlServer;
using LinqToDB.Mapping;

namespace Akka.Persistence.Sql.Linq2Db.Db
{
    public class AkkaPersistenceDataConnectionFactory
    {
        private readonly string _providerName;
        private readonly string _connString;
        private readonly MappingSchema _mappingSchema;
        private readonly LinqToDbConnectionOptions _opts;
        
        public AkkaPersistenceDataConnectionFactory(IProviderConfig<JournalTableConfig> config)
        {
            _providerName = config.ProviderName;
            _connString = config.ConnectionString;
            
            //Build Mapping Schema to be used for all connections.
            //Make a unique mapping schema name here to avoid problems
            //with multiple configurations using different schemas.
            var configName = "akka.persistence.l2db." + HashCode.Combine(config.ConnectionString, config.ProviderName, config.TableConfig.GetHashCode());
            var fmb = new MappingSchema(configName,MappingSchema.Default).GetFluentMappingBuilder();
            MapJournalRow(config, fmb);

            _useCloneDataConnection = config.UseCloneConnection;
            _mappingSchema = fmb.MappingSchema;
            _opts = new LinqToDbConnectionOptionsBuilder()
                .UseConnectionString(_providerName, _connString)
                .UseMappingSchema(_mappingSchema).Build();
            
            if (_providerName.ToLower().StartsWith("sqlserver"))
            {
                _policy = new SqlServerRetryPolicy();
            }
            _cloneConnection = new Lazy<DataConnection>(()=>new DataConnection(_opts));
        }
        
        public AkkaPersistenceDataConnectionFactory(IProviderConfig<SnapshotTableConfiguration> config)
        {
            _providerName = config.ProviderName;
            _connString = config.ConnectionString;
            
            //Build Mapping Schema to be used for all connections.
            //Make a unique mapping schema name here to avoid problems
            //with multiple configurations using different schemas.
            var configName = "akka.persistence.l2db." + HashCode.Combine(config.ConnectionString, config.ProviderName, config.TableConfig.GetHashCode());
            var ms = new MappingSchema(configName, MappingSchema.Default);
            //ms.SetConvertExpression<DateTime, DateTime>(dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc));
            var fmb = ms.GetFluentMappingBuilder();
            MapSnapshotRow(config, fmb);

            _useCloneDataConnection = config.UseCloneConnection;
            _mappingSchema = fmb.MappingSchema;
            _opts = new LinqToDbConnectionOptionsBuilder()
                .UseConnectionString(_providerName, _connString)
                .UseMappingSchema(_mappingSchema).Build();
            
            if (_providerName.ToLower().StartsWith("sqlserver"))
            {
                _policy = new SqlServerRetryPolicy();
            }
            _cloneConnection = new Lazy<DataConnection>(()=>new DataConnection(_opts));
        }

        private static void MapSnapshotRow(
            IProviderConfig<SnapshotTableConfiguration> config,
            FluentMappingBuilder fmb)
        {
            var tableConfig = config.TableConfig;
            var builder = fmb.Entity<SnapshotRow>()
                .HasSchemaName(tableConfig.SchemaName)
                .HasTableName(tableConfig.TableName)
                .Member(r => r.Created)
                .HasColumnName(tableConfig.ColumnNames.Created)
                .Member(r => r.Manifest)
                .HasColumnName(tableConfig.ColumnNames.Manifest)
                .HasLength(500)
                .Member(r => r.Payload)
                .HasColumnName(tableConfig.ColumnNames.Snapshot)
                .Member(r => r.SequenceNumber)
                .HasColumnName(tableConfig.ColumnNames.SequenceNumber)
                .Member(r => r.SerializerId)
                .HasColumnName(tableConfig.ColumnNames.SerializerId)
                .Member(r => r.PersistenceId)
                .HasColumnName(tableConfig.ColumnNames.PersistenceId)
                .HasLength(255);
            
            if (config.ProviderName.ToLower().Contains("sqlite") || config.ProviderName.ToLower().Contains("postgres"))
            {
                builder.Member(r => r.Created)
                    .HasDataType(DataType.Int64)
                    .HasConversion(r => r.Ticks, r => new DateTime(r));
            }
            
            if (config.IDaoConfig.SqlCommonCompatibilityMode)
            {
                
                //builder.Member(r => r.Created)
                //    .HasConversion(l => DateTimeHelpers.FromUnixEpochMillis(l),
                //        dt => DateTimeHelpers.ToUnixEpochMillis(dt));
            }
        }

        private static void MapJournalRow(IProviderConfig<JournalTableConfig> config,
            FluentMappingBuilder fmb)
        {
            var tableConfig = config.TableConfig;
            var journalRowBuilder = fmb.Entity<JournalRow>()
                .HasSchemaName(tableConfig.SchemaName)
                .HasTableName(tableConfig.TableName)
                .Member(r => r.Deleted).HasColumnName(tableConfig.ColumnNames.Deleted)
                .Member(r => r.Manifest).HasColumnName(tableConfig.ColumnNames.Manifest)
                .HasLength(500)
                .Member(r => r.Message).HasColumnName(tableConfig.ColumnNames.Message).IsNullable(false)
                .Member(r => r.Ordering).HasColumnName(tableConfig.ColumnNames.Ordering)
                .Member(r => r.Tags).HasLength(100)
                .HasColumnName(tableConfig.ColumnNames.Tags)
                .Member(r => r.Identifier)
                .HasColumnName(tableConfig.ColumnNames.Identifier)
                .Member(r => r.PersistenceId)
                .HasColumnName(tableConfig.ColumnNames.PersistenceId).HasLength(255).IsNullable(false)
                .Member(r => r.SequenceNumber)
                .HasColumnName(tableConfig.ColumnNames.SequenceNumber)
                .Member(r => r.Timestamp)
                .HasColumnName(tableConfig.ColumnNames.Created);

            //We can skip writing tags the old way by ignoring the column in mapping.
            journalRowBuilder.Member(r => r.TagArr).IsNotColumn();
            if ((tableConfig.TagWriteMode & TagWriteMode.CommaSeparatedArray) == 0)
            {
                journalRowBuilder.Member(r => r.Tags).IsNotColumn();
            }
            
            if (config.ProviderName.ToLower().Contains("sqlite"))
            {
                journalRowBuilder
                    .Member(r => r.Ordering).IsPrimaryKey()
                    .HasDbType("INTEGER")
                    .IsIdentity();
            }
            else
            {
                journalRowBuilder
                    .Member(r => r.Ordering).IsIdentity()
                    .Member(r=>r.PersistenceId).IsPrimaryKey()
                    .Member(r=>r.SequenceNumber).IsPrimaryKey();
            }
            
            void SetJoinCol<T>(PropertyMappingBuilder<JournalTagRow, T> builder,
                PropertyMappingBuilder<JournalRow, long> propertyMappingBuilder)
            {
                if (config.TableConfig.TagTableMode ==
                    TagTableMode.SequentialUUID)
                {
                    builder.Member(r => r.JournalOrderingId)
                        .IsNotColumn()
                        .Member(r => r.WriteUUID)
                        .IsColumn().IsPrimaryKey();
                    journalRowBuilder.Member(r => r.WriteUUID)
                        .IsColumn();
                }
                else
                {
                    builder.Member(r => r.WriteUUID)
                        .IsNotColumn()
                        .Member(r => r.JournalOrderingId)
                        .IsColumn().IsPrimaryKey();
                    journalRowBuilder.Member(r => r.WriteUUID)
                        .IsNotColumn();
                }
            }

            if (config.TableConfig.UseEventManifestColumn)
            {
                journalRowBuilder.Member(r => r.eventManifest)
                    .IsColumn().HasLength(64);
            }
            else
            {
                journalRowBuilder.Member(r => r.eventManifest)
                    .IsNotColumn();   
            }
            if ((config.TableConfig.TagWriteMode & TagWriteMode.TagTable) != 0)
            {
                var tagTableBuilder = fmb.Entity<JournalTagRow>()
                    .HasTableName(tableConfig.TagTableName)
                    .HasSchemaName(tableConfig.SchemaName)
                    .Member(r => r.TagValue)
                    .IsColumn().IsNullable(false)
                    .HasLength(64)
                    .IsPrimaryKey();
                SetJoinCol(tagTableBuilder, journalRowBuilder);
            }
            
            //Probably overkill, but we only set Metadata Mapping if specified
            //That we are in delete compatibility mode.
            if (config.IDaoConfig.SqlCommonCompatibilityMode)
            {
                fmb.Entity<JournalMetaData>()
                    .HasTableName(tableConfig.MetadataTableName)
                    .HasSchemaName(tableConfig.SchemaName)
                    .Member(r => r.PersistenceId)
                    .HasColumnName(tableConfig.MetadataColumnNames.PersistenceId)
                    .HasLength(255)
                    .Member(r => r.SequenceNumber)
                    .HasColumnName(tableConfig.MetadataColumnNames.SequenceNumber);
            }
        }

        private readonly Lazy<DataConnection> _cloneConnection;
        private readonly bool _useCloneDataConnection;
        private readonly IRetryPolicy _policy;

        public DataConnection GetConnection()
        {
            if (_useCloneDataConnection)
            {
                var conn = (DataConnection)_cloneConnection.Value.Clone();
                conn.RetryPolicy = _policy;
                return conn;
            }
            
            return new DataConnection(_opts) { RetryPolicy = _policy};
        }
    }
}