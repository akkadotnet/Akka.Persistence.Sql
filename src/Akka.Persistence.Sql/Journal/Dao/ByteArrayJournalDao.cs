// -----------------------------------------------------------------------
//  <copyright file="ByteArrayJournalDao.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
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
            string selfUuid)
            : base(
                scheduler: scheduler,
                materializer: mat,
                connectionFactory: connection,
                config: journalConfig,
                serializer: serializer,
                logger: logger,
                selfUuid: selfUuid) { }

        // TODO: change this to async
        public void InitializeTables()
        {
            using var connection = ConnectionFactory.GetConnection();

            try
            {
                connection.CreateTable<JournalRow>();
                if (JournalConfig.PluginConfig.TagMode is not TagMode.Csv)
                    connection.CreateTable<JournalTagRow>();
            }
            catch (Exception e)
            {
                if (JournalConfig.WarnOnAutoInitializeFail)
                {
                    Logger.Warning(
                        e,
                        $"Could not Create Journal Table {JournalConfig.TableConfig.EventJournalTable.Name} as requested by config.");
                }
            }

            if (JournalConfig.DaoConfig.SqlCommonCompatibilityMode)
            {
                try
                {
                    connection.CreateTable<JournalMetaData>();
                }
                catch (Exception e)
                {
                    if (JournalConfig.WarnOnAutoInitializeFail)
                    {
                        Logger.Warning(
                            e,
                            $"Could not Create Journal Metadata Table {JournalConfig.TableConfig.MetadataTable.Name} as requested by config.");
                    }
                }
            }
        }
    }
}
