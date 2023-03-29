// -----------------------------------------------------------------------
//  <copyright file="ByteArrayJournalDao.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Streams;
using LinqToDB;

namespace Akka.Persistence.Sql.Journal.Dao
{
    public sealed class ByteArrayJournalDao : BaseByteArrayJournalDao
    {
        public ByteArrayJournalDao(
            IAdvancedScheduler scheduler,
            IMaterializer mat,
            AkkaPersistenceDataConnectionFactory connection,
            JournalConfig journalConfig,
            Akka.Serialization.Serialization serializer,
            ILoggingAdapter logger,
            CancellationToken shutdownToken)
            : base(
                scheduler: scheduler,
                materializer: mat,
                connectionFactory: connection,
                config: journalConfig,
                serializer: new ByteArrayJournalSerializer(
                    journalConfig,
                    serializer,
                    journalConfig.PluginConfig.TagSeparator),
                logger: logger,
                shutdownToken: shutdownToken) { }

        public async Task InitializeTables(CancellationToken token)
        {
            await using var connection = ConnectionFactory.GetConnection();

            // MS Sqlite does not support schema, we have to blindly try and create the tables
            if (connection.DataProvider.Name is ProviderName.SQLiteMS)
            {
                try
                {
                    await connection.CreateTableAsync<JournalRow>(token);
                }
                catch { /* no-op */ }
                
                if (JournalConfig.PluginConfig.TagMode is not TagMode.Csv)
                    try
                    {
                        await connection.CreateTableAsync<JournalTagRow>(token);
                    }
                    catch { /* no-op */ }
                
                if (JournalConfig.DaoConfig.SqlCommonCompatibilityMode)
                    try
                    {
                        await connection.CreateTableAsync<JournalMetaData>(token);
                    }
                    catch { /* no-op */ }

                return;
            }

            var schema = connection.GetSchema();
            if(schema.Tables.All(t => t.TableName != JournalConfig.TableConfig.EventJournalTable.Name))
                await connection.CreateTableAsync<JournalRow>(token);
            
            if (JournalConfig.PluginConfig.TagMode is not TagMode.Csv)
                if(schema.Tables.All(t => t.TableName != JournalConfig.TableConfig.TagTable.Name))
                    await connection.CreateTableAsync<JournalTagRow>(token);

            if (JournalConfig.DaoConfig.SqlCommonCompatibilityMode)
                if(schema.Tables.All(t => t.TableName != JournalConfig.TableConfig.MetadataTable.Name))
                    await connection.CreateTableAsync<JournalMetaData>(token);
        }
    }
}
