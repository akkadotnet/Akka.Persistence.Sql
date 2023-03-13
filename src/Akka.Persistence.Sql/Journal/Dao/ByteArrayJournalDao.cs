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
            ILoggingAdapter logger)
            : base(
                scheduler: scheduler,
                materializer: mat,
                connectionFactory: connection,
                config: journalConfig,
                serializer: new ByteArrayJournalSerializer(journalConfig, serializer, journalConfig.PluginConfig.TagSeparator),
                logger: logger)
        {
        }

        // TODO: change this to async
        public void InitializeTables()
        {
            using var conn = ConnectionFactory.GetConnection();

            try
            {
                conn.CreateTable<JournalRow>();
                if(JournalConfig.TableConfig.TagWriteMode is not TagWriteMode.Csv)
                    conn.CreateTable<JournalTagRow>();
            }
            catch (Exception e)
            {
                if (JournalConfig.WarnOnAutoInitializeFail)
                {
                    Logger.Warning(e,$"Could not Create Journal Table {JournalConfig.TableConfig.EventJournalTable.Name} as requested by config.");
                }
            }

            if (JournalConfig.DaoConfig.SqlCommonCompatibilityMode)
            {
                try
                {
                    conn.CreateTable<JournalMetaData>();
                }
                catch (Exception e)
                {
                    if (JournalConfig.WarnOnAutoInitializeFail)
                    {
                        Logger.Warning(e,$"Could not Create Journal Metadata Table {JournalConfig.TableConfig.MetadataTable.Name} as requested by config.");
                    }
                }
            }
        }
    }
}
