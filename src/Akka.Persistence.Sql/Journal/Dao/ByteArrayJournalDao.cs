// -----------------------------------------------------------------------
//  <copyright file="ByteArrayJournalDao.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

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
            string selfUuid,
            CancellationToken shutdownToken)
            : base(
                scheduler: scheduler,
                materializer: mat,
                connectionFactory: connection,
                config: journalConfig,
                serializer: serializer,
                logger: logger,
                selfUuid: selfUuid,
                shutdownToken: shutdownToken) { }

        public async Task InitializeTables(CancellationToken token)
        {
            await using var connection = ConnectionFactory.GetConnection();

            var journalFooter = JournalConfig.GenerateJournalFooter();
            await connection.CreateTableAsync<JournalRow>(TableOptions.CreateIfNotExists, journalFooter, token);

            if (JournalConfig.PluginConfig.TagMode is not TagMode.Csv)
            {
                var tagFooter = JournalConfig.GenerateTagFooter();
                await connection.CreateTableAsync<JournalTagRow>(TableOptions.CreateIfNotExists, tagFooter, token);
            }

            if (JournalConfig.DaoConfig.SqlCommonCompatibilityMode)
                await connection.CreateTableAsync<JournalMetaData>(TableOptions.CreateIfNotExists, null, token);
        }
    }
}
