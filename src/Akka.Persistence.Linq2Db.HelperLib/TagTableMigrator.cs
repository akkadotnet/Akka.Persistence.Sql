// -----------------------------------------------------------------------
//  <copyright file="TagTableMigrator.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db;
using Akka.Persistence.Sql.Linq2Db.Config;
using Akka.Persistence.Sql.Linq2Db.Db;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Tools;

namespace Akka.Persistence.Linq2Db.HelperLib
{
    public class TagTableMigrator
    {
        private readonly AkkaPersistenceDataConnectionFactory _connectionFactory;
        private readonly JournalConfig _journalConfig;
        private readonly string _separator;

        public TagTableMigrator(Config config)
        {
            config = config
                .WithFallback(Linq2DbPersistence.DefaultConfiguration)
                .GetConfig("akka.persistence.journal.linq2db");
            
            var mapping = config.GetString("table-mapping");
            if(string.IsNullOrWhiteSpace(mapping) || mapping == "default")
                throw new ConfigurationException("akka.persistence.journal.linq2db.table-mapping must not be empty or 'default'");
            
            _journalConfig = new JournalConfig(config);
            if (_journalConfig.TableConfig.TagWriteMode != TagWriteMode.Both)
                throw new ConfigurationException("akka.persistence.journal.linq2db.tag-write-mode has to be 'Both'");

            _connectionFactory = new AkkaPersistenceDataConnectionFactory(_journalConfig);
            _separator = _journalConfig.PluginConfig.TagSeparator;
        }

        public async Task Migrate(long startOffset, int batchSize, long? endOffset = null)
        {
            var config = _journalConfig.DaoConfig;
            
            await using var db = _connectionFactory.GetConnection();

            // Create the tag table if it doesn't exist
            var schemaProvider = db.DataProvider.GetSchemaProvider();
            var dbSchema = schemaProvider.GetSchema(db);
            if (dbSchema.Tables.All(t => t.TableName != _journalConfig.TableConfig.TagTable.Name))
            {
                await db.CreateTableAsync<JournalTagRow>();
            }
            
            long maxId;
            if (endOffset is null)
            {
                var jtrQuery = db.GetTable<JournalTagRow>()
                    .Select(jtr => jtr.OrderingId)
                    .Distinct();

                maxId = await db.GetTable<JournalRow>()
                    .Where(r =>
                        r.Tags != null
                        && r.Tags.Length > 0
                        && r.Ordering.NotIn(jtrQuery))
                    .Select(r => r.Ordering)
                    .OrderByDescending(r => r)
                    .FirstOrDefaultAsync();
            }
            else
            {
                maxId = endOffset.Value;
            }

            while (startOffset <= maxId)
            {
                await using (var tx = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted))
                {
                    try
                    {
                        var offset = startOffset;
                        var rows = await db.GetTable<JournalRow>()
                            .Where(r =>
                                r.Ordering >= offset
                                && r.Ordering < offset + batchSize
                                && r.Tags != null
                                && r.Tags.Length > 0)
                            .ToListAsync();

                        var tagList = new List<JournalTagRow>();
                        foreach (var row in rows)
                        {
                            var tags = row.Tags
                                .Split(new [] {_separator}, StringSplitOptions.RemoveEmptyEntries)
                                .Where(s => !string.IsNullOrWhiteSpace(s));
                            
                            tagList.AddRange(tags.Select(tag => new JournalTagRow
                            {
                                OrderingId = row.Ordering,
                                TagValue = tag,
                                SequenceNumber = row.SequenceNumber,
                                PersistenceId = row.PersistenceId
                            }));
                        }

                        await db.GetTable<JournalTagRow>().BulkCopyAsync(new BulkCopyOptions
                        {
                            BulkCopyType = BulkCopyType.MultipleRows,
                            UseParameters = config.PreferParametersOnMultiRowInsert,
                            MaxBatchSize = config.DbRoundTripTagBatchSize
                        }, tagList);

                        await tx.CommitAsync();
                    }
                    catch (Exception e1)
                    {
                        try
                        {
                            await tx.RollbackAsync();
                        }
                        catch (Exception e2)
                        {
                            throw new AggregateException(
                                $"Migration failed on offset {startOffset} to {startOffset + batchSize}, Rollback failed.",
                                e2, e1);
                        }
                        throw new Exception(
                            $"Migration failed on offset {startOffset} to {startOffset + batchSize}, Rollback successful.", 
                            e1);
                    }
                }
                startOffset += batchSize;
            }
        }
    }
}