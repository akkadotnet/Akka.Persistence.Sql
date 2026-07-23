// -----------------------------------------------------------------------
//  <copyright file="SqlServerTransactionRetrySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Extensions;
using Akka.Persistence.Sql.Journal.Dao;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Streams;
using FluentAssertions;
using LinqToDB;
using LinqToDB.Data.RetryPolicy;
using LinqToDB.Interceptors;
using LinqToDB.Mapping;
using Microsoft.Data.SqlClient;
using Xunit;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.SqlServer;

#if !DEBUG
[SkipWindows]
#endif
[Collection(nameof(SqlServerPersistenceSpec))]
public sealed class SqlServerTransactionRetrySpec
{
    private const int StressIterations = 10;
    private readonly SqlServerContainer _fixture;

    public SqlServerTransactionRetrySpec(SqlServerContainer fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Public_transaction_handler_should_keep_its_configured_command_retry_policy()
    {
        var retryPolicy = new PassThroughRetryPolicy();
        var config = WithRetryPolicy(CreateConfig(), retryPolicy);
        var factory = new AkkaPersistenceDataConnectionFactory(config);
        IRetryPolicy? handlerPolicy = null;

        await factory.ExecuteWithTransactionAsync(
            IsolationLevel.ReadCommitted,
            CancellationToken.None,
            (connection, _) =>
            {
                handlerPolicy = connection.DetachRetryPolicy();
                return Task.CompletedTask;
            });

        handlerPolicy.Should().BeSameAs(retryPolicy);
    }

    [Fact]
    public async Task Public_transaction_handler_should_execute_only_once()
    {
        var factory = new AkkaPersistenceDataConnectionFactory(CreateConfig());
        var invocationCount = 0;

        var action = () => factory.ExecuteWithTransactionAsync(
            IsolationLevel.ReadCommitted,
            CancellationToken.None,
            (_, _) =>
            {
                Interlocked.Increment(ref invocationCount);
                throw new TimeoutException("The public callback must not become implicitly replayable.");
            });

        await action.Should().ThrowAsync<TimeoutException>();
        invocationCount.Should().Be(1);
    }

    [Fact]
    public async Task Configured_retry_policy_should_own_complete_transaction_attempts()
    {
        var retryPolicy = new ReplayTwiceRetryPolicy();
        var factory = new AkkaPersistenceDataConnectionFactory(WithRetryPolicy(CreateConfig(), retryPolicy));
        var connections = new ConcurrentBag<AkkaDataConnection>();

        await factory.ExecuteReplaySafeWithTransactionRetryAsync(
            IsolationLevel.ReadCommitted,
            CancellationToken.None,
            (connection, _) =>
            {
                connections.Add(connection);
                connection.DetachRetryPolicy().Should().BeNull(
                    "command retry must be outside the transaction it may replace");
                return Task.CompletedTask;
            });

        retryPolicy.VoidAsyncAttempts.Should().Be(2);
        connections.Distinct(ReferenceEqualityComparer.Instance).Should().HaveCount(2);
    }

    [Fact]
    public async Task Connection_cleanup_failure_should_not_hide_the_retryable_operation_failure()
    {
        var retryPolicy = new RetryOncePolicy();
        var options = CreateDataOptions(retryPolicy)
            .UseInterceptor(new ThrowOnFirstClosingInterceptor());
        var factory = new AkkaPersistenceDataConnectionFactory(
            CreateConfig().WithDataOptions(options));
        var invocationCount = 0;

        await factory.ExecuteReplaySafeWithTransactionRetryAsync(
            IsolationLevel.ReadCommitted,
            CancellationToken.None,
            (_, _) =>
            {
                if (Interlocked.Increment(ref invocationCount) == 1)
                    throw new RetryableTestException();

                return Task.CompletedTask;
            });

        retryPolicy.VoidAsyncAttempts.Should().Be(2);
        retryPolicy.FirstException.Should().BeOfType<RetryableTestException>();
        retryPolicy.FirstException!.Data.Values
            .Cast<object>()
            .Should().ContainSingle(value => value is ConnectionCleanupTestException);
    }

    [Fact]
    public async Task Deadlock_victim_should_retry_entire_transaction_on_fresh_connection()
    {
        var config = CreateConfig();
        var factory = new AkkaPersistenceDataConnectionFactory(config);

        await using (var connection = factory.GetConnection())
        {
            await connection.CreateTableAsync<DeadlockRow>(TableOptions.None, null, CancellationToken.None);
            await connection.GetTable<DeadlockRow>().InsertAsync(() => new DeadlockRow { Id = 1, Value = 0 });
            await connection.GetTable<DeadlockRow>().InsertAsync(() => new DeadlockRow { Id = 2, Value = 0 });
        }

        for (var iteration = 0; iteration < StressIterations; iteration++)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var bothFirstRowsLocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstLockCount = 0;
            var invocationCount = 0;
            var connections = new ConcurrentBag<AkkaDataConnection>();

            Task Execute(int firstRowId, int secondRowId)
                => factory.ExecuteReplaySafeWithTransactionRetryAsync(
                    IsolationLevel.ReadCommitted,
                    timeout.Token,
                    async (connection, token) =>
                    {
                        connections.Add(connection);
                        Interlocked.Increment(ref invocationCount);

                        await LockRow(connection, firstRowId, token);
                        if (Interlocked.Increment(ref firstLockCount) == 2)
                            bothFirstRowsLocked.SetResult();

                        await bothFirstRowsLocked.Task.WaitAsync(token);
                        await LockRow(connection, secondRowId, token);
                    });

            await Task.WhenAll(
                Execute(1, 2),
                Execute(2, 1));

            invocationCount.Should().Be(3, "the deadlock victim should rerun the complete transaction handler once");
            connections.Distinct(ReferenceEqualityComparer.Instance).Should().HaveCount(3);
        }

        await using var verificationConnection = factory.GetConnection();
        var rows = await verificationConnection.GetTable<DeadlockRow>()
            .OrderBy(row => row.Id)
            .ToArrayAsync();
        rows.Select(row => row.Value).Should().Equal(StressIterations * 2, StressIterations * 2);
    }

    [Fact]
    public async Task Journal_dao_delete_should_recover_from_deadlock_without_completed_transaction_failure()
    {
        const string journalTableName = "DaoTransactionRetryJournal";
        const string metadataTableName = journalTableName + "Metadata";
        var config = CreateConfig(journalTableName);
        var factory = new AkkaPersistenceDataConnectionFactory(config);
        var actorSystem = ActorSystem.Create("dao-transaction-retry-spec");

        try
        {
            var dao = new ByteArrayJournalDao(
                actorSystem.Scheduler.Advanced,
                actorSystem.Materializer(),
                factory,
                config,
                actorSystem.Serialization,
                Logging.GetLogger(actorSystem, nameof(SqlServerTransactionRetrySpec)),
                null,
                CancellationToken.None);

            await dao.InitializeTables(CancellationToken.None);
            await CreateMetadataDeadlockTrigger(metadataTableName);

            for (var iteration = 0; iteration < StressIterations; iteration++)
            {
                var firstPersistenceId = $"p-{iteration}-1";
                var secondPersistenceId = $"p-{iteration}-2";
                await SeedJournalRows(factory, firstPersistenceId, secondPersistenceId);

                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                async Task DeleteWhenStarted(string persistenceId)
                {
                    await start.Task.WaitAsync(timeout.Token);
                    await dao.Delete(persistenceId, 2, timeout.Token);
                }

                var firstDelete = DeleteWhenStarted(firstPersistenceId);
                var secondDelete = DeleteWhenStarted(secondPersistenceId);
                start.SetResult();

                await Task.WhenAll(firstDelete, secondDelete);
            }

            await using var verificationConnection = factory.GetConnection();
            var remainingRows = await verificationConnection.GetTable<JournalRow>()
                .OrderBy(row => row.PersistenceId)
                .ThenBy(row => row.SequenceNumber)
                .ToArrayAsync();

            remainingRows.Should().HaveCount(StressIterations * 2)
                .And.OnlyContain(row => row.SequenceNumber == 2 && row.Deleted);

            var metadataRows = await verificationConnection.GetTable<JournalMetaData>()
                .OrderBy(row => row.PersistenceId)
                .ToArrayAsync();

            metadataRows.Should().HaveCount(StressIterations * 2)
                .And.OnlyContain(row => row.SequenceNumber == 2);

            var tagRows = await verificationConnection.GetTable<JournalTagRow>()
                .OrderBy(row => row.PersistenceId)
                .ThenBy(row => row.SequenceNumber)
                .ToArrayAsync();

            tagRows.Should().HaveCount(StressIterations * 2)
                .And.OnlyContain(row => row.SequenceNumber == 2);
        }
        finally
        {
            await actorSystem.Terminate();
        }
    }

    [Fact]
    public async Task Journal_dao_delete_should_remain_consistent_when_policy_replays_the_complete_transaction()
    {
        const string journalTableName = "DaoTransactionReplayJournal";
        const string persistenceId = "replayed-delete";
        var baseConfig = CreateConfig(journalTableName);
        var baseFactory = new AkkaPersistenceDataConnectionFactory(baseConfig);
        var actorSystem = ActorSystem.Create("dao-transaction-replay-spec");

        try
        {
            var initializationDao = CreateDao(actorSystem, baseFactory, baseConfig);
            await initializationDao.InitializeTables(CancellationToken.None);
            await SeedJournalRows(baseFactory, persistenceId);

            var retryPolicy = new ReplayTwiceRetryPolicy();
            var policyFactoryCalls = 0;
            var retryOptions = new DataOptions()
                .UseConnectionString(_fixture.ProviderName, _fixture.ConnectionString);
            retryOptions = retryOptions.WithOptions(retryOptions.RetryPolicyOptions with
            {
                Factory = _ => Interlocked.Increment(ref policyFactoryCalls) == 2
                    ? retryPolicy
                    : new PassThroughRetryPolicy(),
            });
            var retryConfig = baseConfig.WithDataOptions(retryOptions);
            var retryFactory = new AkkaPersistenceDataConnectionFactory(retryConfig);
            var retryDao = CreateDao(actorSystem, retryFactory, retryConfig);

            await retryDao.Delete(persistenceId, 2, CancellationToken.None);

            retryPolicy.VoidAsyncAttempts.Should().Be(2,
                "the adversarial policy deliberately executes the complete transaction twice");

            await using var verificationConnection = baseFactory.GetConnection();
            var remainingRows = await verificationConnection.GetTable<JournalRow>()
                .Where(row => row.PersistenceId == persistenceId)
                .ToArrayAsync();
            remainingRows.Should().ContainSingle(row => row.SequenceNumber == 2 && row.Deleted);

            var metadataRows = await verificationConnection.GetTable<JournalMetaData>()
                .Where(row => row.PersistenceId == persistenceId)
                .ToArrayAsync();
            metadataRows.Should().ContainSingle(row => row.SequenceNumber == 2);

            var tagRows = await verificationConnection.GetTable<JournalTagRow>()
                .Where(row => row.PersistenceId == persistenceId)
                .ToArrayAsync();
            tagRows.Should().ContainSingle(row => row.SequenceNumber == 2);
        }
        finally
        {
            await actorSystem.Terminate();
        }
    }

    private JournalConfig CreateConfig(string journalTableName = "journal")
    {
        var config = ConfigurationFactory.ParseString(
                $$"""
                  akka.persistence.journal.sql {
                    connection-string = "{{_fixture.ConnectionString}}"
                    provider-name = "{{_fixture.ProviderName}}"
                    delete-compatibility-mode = true
                    tag-write-mode = TagTable
                    default.journal {
                      table-name = "{{journalTableName}}"
                      use-writer-uuid-column = false
                    }
                    default.metadata.table-name = "{{journalTableName}}Metadata"
                    default.tag.table-name = "{{journalTableName}}Tags"
                  }
                  """)
            .WithFallback(SqlPersistence.DefaultConfiguration)
            .GetConfig("akka.persistence.journal.sql");

        return new JournalConfig(config);
    }

    private DataOptions CreateDataOptions(IRetryPolicy retryPolicy)
        => new DataOptions()
            .UseConnectionString(_fixture.ProviderName, _fixture.ConnectionString)
            .UseRetryPolicy(retryPolicy);

    private JournalConfig WithRetryPolicy(JournalConfig config, IRetryPolicy retryPolicy)
        => config.WithDataOptions(CreateDataOptions(retryPolicy));

    private static ByteArrayJournalDao CreateDao(
        ActorSystem actorSystem,
        AkkaPersistenceDataConnectionFactory factory,
        JournalConfig config)
        => new(
            actorSystem.Scheduler.Advanced,
            actorSystem.Materializer(),
            factory,
            config,
            actorSystem.Serialization,
            Logging.GetLogger(actorSystem, nameof(SqlServerTransactionRetrySpec)),
            null,
            CancellationToken.None);

    private static async Task SeedJournalRows(
        AkkaPersistenceDataConnectionFactory factory,
        params string[] persistenceIds)
    {
        await using var connection = factory.GetConnection();
        foreach (var persistenceId in persistenceIds)
        {
            for (var sequenceNumber = 1L; sequenceNumber <= 2; sequenceNumber++)
            {
                var orderingId = await connection.InsertWithInt64IdentityAsync(
                    CreateJournalRow(persistenceId, sequenceNumber));

                await connection.GetTable<JournalTagRow>().InsertAsync(
                    () => new JournalTagRow
                    {
                        OrderingId = orderingId,
                        PersistenceId = persistenceId,
                        SequenceNumber = sequenceNumber,
                        TagValue = "delete-retry",
                    });
            }
        }
    }

    private static JournalRow CreateJournalRow(string persistenceId, long sequenceNumber)
        => new()
        {
            PersistenceId = persistenceId,
            SequenceNumber = sequenceNumber,
            Timestamp = DateTime.UtcNow.Ticks,
            Message = [1],
            Manifest = string.Empty,
            Tags = string.Empty,
        };

    private async Task CreateMetadataDeadlockTrigger(string metadataTableName)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
              CREATE TABLE [DaoDeleteDeadlockControl]
              (
                  [Id] int NOT NULL PRIMARY KEY,
                  [Value] int NOT NULL
              );

              INSERT INTO [DaoDeleteDeadlockControl] ([Id], [Value]) VALUES (1, 0), (2, 0);
              """;
        await command.ExecuteNonQueryAsync();

        command.CommandText =
            $$"""
              CREATE TRIGGER [DaoMetadataDeadlockTrigger]
              ON [{{metadataTableName}}]
              AFTER INSERT, UPDATE
              AS
              BEGIN
                  SET NOCOUNT ON;

                  DECLARE @persistenceId nvarchar(255) =
                      (SELECT TOP (1) [persistence_id] FROM inserted);
                  DECLARE @firstId int = CASE WHEN RIGHT(@persistenceId, 1) = N'1' THEN 1 ELSE 2 END;
                  DECLARE @secondId int = CASE WHEN @firstId = 1 THEN 2 ELSE 1 END;

                  UPDATE [DaoDeleteDeadlockControl] WITH (ROWLOCK)
                  SET [Value] = [Value] + 1
                  WHERE [Id] = @firstId;

                  WAITFOR DELAY '00:00:01';

                  UPDATE [DaoDeleteDeadlockControl] WITH (ROWLOCK)
                  SET [Value] = [Value] + 1
                  WHERE [Id] = @secondId;
              END;
              """;
        await command.ExecuteNonQueryAsync();
    }

    private static Task<int> LockRow(
        AkkaDataConnection connection,
        int rowId,
        CancellationToken cancellationToken)
        => connection.GetTable<DeadlockRow>()
            .Where(row => row.Id == rowId)
            .Set(row => row.Value, row => row.Value + 1)
            .UpdateAsync(cancellationToken);

    [Table("TransactionRetryDeadlock")]
    private sealed class DeadlockRow
    {
        [PrimaryKey]
        public int Id { get; set; }

        [Column]
        public int Value { get; set; }
    }

    private class PassThroughRetryPolicy : IRetryPolicy
    {
        public int ResultAsyncAttempts { get; protected set; }
        public int VoidAsyncAttempts { get; protected set; }

        public TResult Execute<TResult>(Func<TResult> operation)
            => operation();

        public void Execute(Action operation)
            => operation();

        public virtual async Task<TResult> ExecuteAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            CancellationToken cancellationToken = default)
        {
            ResultAsyncAttempts++;
            return await operation(cancellationToken);
        }

        public virtual async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            VoidAsyncAttempts++;
            await operation(cancellationToken);
        }
    }

    private sealed class ReplayTwiceRetryPolicy : PassThroughRetryPolicy
    {
        public override async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            await base.ExecuteAsync(operation, cancellationToken);
            await base.ExecuteAsync(operation, cancellationToken);
        }
    }

    private sealed class RetryOncePolicy : PassThroughRetryPolicy
    {
        public Exception? FirstException { get; private set; }

        public override async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default)
        {
            try
            {
                await base.ExecuteAsync(operation, cancellationToken);
            }
            catch (RetryableTestException exception)
            {
                FirstException = exception;
                await base.ExecuteAsync(operation, cancellationToken);
            }
        }
    }

    private sealed class ThrowOnFirstClosingInterceptor : DataContextInterceptor
    {
        private int _closingCount;

        public override Task OnClosingAsync(DataContextEventData eventData)
        {
            if (Interlocked.Increment(ref _closingCount) == 1)
                throw new ConnectionCleanupTestException();

            return Task.CompletedTask;
        }
    }

    private sealed class RetryableTestException : Exception
    {
    }

    private sealed class ConnectionCleanupTestException : Exception
    {
    }
}
