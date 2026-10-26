using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Pattern;
using Akka.Persistence.Journal;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Dao;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Streams;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Common;
using LinqToDB.Interceptors;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Sqlite;

public class SqliteWriteQueueCompletionSpec : Akka.TestKit.Xunit.TestKit
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    private readonly ITestOutputHelper _output;

    public SqliteWriteQueueCompletionSpec(ITestOutputHelper output)
        : base(SqlPersistence.DefaultConfiguration.ToString(), output) => _output = output;

    [Theory]
    [InlineData(TagMode.Csv, WriteQueueMode.SourceQueue)]
    [InlineData(TagMode.TagTable, WriteQueueMode.SourceQueue)]
    [InlineData(TagMode.Csv, WriteQueueMode.Channel)]
    [InlineData(TagMode.TagTable, WriteQueueMode.Channel)]
    public async Task Shutdown_releases_a_write_waiting_behind_an_active_database_operation(TagMode mode, WriteQueueMode queueMode)
    {
        await using var fixture = await CreateFixture(mode, queueMode);
        var active = fixture.Write("active");
        await fixture.Gate.Entered.Task.WaitAsync(Deadline);
        var queued = fixture.Write("queued");
        await fixture.AdmissionBarrier();

        fixture.Dao.CompleteWriteQueue();
        fixture.Shutdown.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(Deadline));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active.WaitAsync(Deadline));
        Assert.False(fixture.Gate.Release.Task.IsCompleted);
        Assert.Equal(0, await fixture.RowCount());
    }

    [Theory]
    [InlineData(TagMode.Csv, WriteQueueMode.SourceQueue)]
    [InlineData(TagMode.TagTable, WriteQueueMode.SourceQueue)]
    [InlineData(TagMode.Csv, WriteQueueMode.Channel)]
    [InlineData(TagMode.TagTable, WriteQueueMode.Channel)]
    public async Task Completing_the_queue_drains_accepted_writes_and_rejects_later_offers(TagMode mode, WriteQueueMode queueMode)
    {
        await using var fixture = await CreateFixture(mode, queueMode);
        var active = fixture.Write("active");
        await fixture.Gate.Entered.Task.WaitAsync(Deadline);
        var queued = fixture.Write("queued");
        await fixture.AdmissionBarrier();

        fixture.Dao.CompleteWriteQueue();
        fixture.Gate.Release.TrySetResult(true);

        await Task.WhenAll(active, queued).WaitAsync(Deadline);
        Assert.Equal(2, await fixture.RowCount());
        if (mode == TagMode.TagTable)
            Assert.Equal(4, await fixture.TagCount());
        var rejected = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Write("later").WaitAsync(Deadline));
        Assert.IsNotType<TimeoutException>(rejected);
    }

    [Theory]
    [InlineData(WriteQueueMode.SourceQueue)]
    [InlineData(WriteQueueMode.Channel)]
    public async Task Database_failure_is_still_reported_when_shutdown_has_not_started(WriteQueueMode queueMode)
    {
        await using var fixture = await CreateFixture(TagMode.Csv, queueMode);
        fixture.Gate.Release.TrySetResult(true);
        await fixture.Write("duplicate").WaitAsync(Deadline);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Write("duplicate").WaitAsync(Deadline));

        Assert.IsNotType<TimeoutException>(failure);
        Assert.IsNotAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(1, await fixture.RowCount());
    }

    private async Task<QueueFixture> CreateFixture(TagMode mode, WriteQueueMode queueMode)
    {
        var fixture = new QueueFixture(Sys, mode, queueMode, _output);
        await fixture.Dao.InitializeTables(CancellationToken.None);
        return fixture;
    }

    private sealed class QueueFixture : IAsyncDisposable
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"sql-write-queue-{Guid.NewGuid():N}.db");
        private readonly AkkaPersistenceDataConnectionFactory _factory;
        private readonly List<Task> _writes = new();
        private Task _drained = Task.CompletedTask;
        private readonly ITestOutputHelper _output;

        public QueueFixture(ActorSystem system, TagMode mode, WriteQueueMode queueMode, ITestOutputHelper output)
        {
            _output = output;
            var config = ConfigurationFactory.ParseString($$"""
                provider-name = SQLite.MS
                connection-string = "Data Source={{_databasePath.Replace('\\', '/')}};Pooling=False"
                tag-write-mode = {{mode}}
                use-clone-connection = false
                parallelism = 1
                batch-size = 1
                write-queue-mode = {{(queueMode == WriteQueueMode.Channel ? "channel" : "source-queue")}}
                """).WithFallback(SqlPersistence.DefaultJournalConfiguration);
            var journalConfig = new JournalConfig(config);
            var options = new DataOptions()
                .UseConnectionString(journalConfig.ProviderName, journalConfig.ConnectionString)
                .UseInterceptor(Gate);
            _factory = new AkkaPersistenceDataConnectionFactory(journalConfig.WithDataOptions(options));
            Materializer = ActorMaterializer.Create(system, ActorMaterializerSettings.Create(system));
            Dao = new ByteArrayJournalDao(system.Scheduler.Advanced, Materializer, _factory, journalConfig,
                system.Serialization, Logging.GetLogger(system, nameof(QueueFixture)), "queue-test", Shutdown.Token);
            var queueField = typeof(BaseByteArrayJournalDao).GetField("WriteQueue", BindingFlags.Instance | BindingFlags.NonPublic);
            var channelField = typeof(BaseByteArrayJournalDao).GetField("_writeChannel", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(queueField);
            Assert.NotNull(channelField);
            if (queueMode == WriteQueueMode.Channel)
            {
                Assert.Null(queueField.GetValue(Dao));
                Channel = Assert.IsAssignableFrom<Channel<WriteQueueEntry>>(channelField.GetValue(Dao));
            }
            else
            {
                Assert.Null(channelField.GetValue(Dao));
                Queue = Assert.IsAssignableFrom<ISourceQueueWithComplete<WriteQueueEntry>>(queueField.GetValue(Dao));
            }
        }

        public ByteArrayJournalDao Dao { get; }
        public ISourceQueueWithComplete<WriteQueueEntry>? Queue { get; }
        public Channel<WriteQueueEntry>? Channel { get; }
        public ActorMaterializer Materializer { get; }
        public CancellationTokenSource Shutdown { get; } = new();
        public InsertGate Gate { get; } = new();

        public Task<IImmutableList<Exception>> Write(string persistenceId, CancellationToken token = default)
        {
            var payload = new Tagged(new byte[] { 1, 2, 3 }, ImmutableHashSet.Create("a", "b"));
            var task = Dao.AsyncWriteMessages(new[] { new AtomicWrite(new Persistent(payload, 1, persistenceId)) }, token);
            _writes.Add(task);
            return task;
        }

        public async Task AdmissionBarrier()
        {
            // Offer callbacks are processed in order. Acceptance of this marker proves
            // that the preceding application request has passed queue admission too.
            // Channel writes are admitted synchronously, so there the marker only tracks draining.
            var marker = new WriteQueueEntry(new TaskCompletionSource<NotUsed>(TaskCreationOptions.RunContinuationsAsynchronously),
                LanguageExt.Seq<JournalRow>.Empty, CancellationToken.None);
            if (Channel is not null)
                Assert.True(Channel.Writer.TryWrite(marker));
            else
                Assert.IsType<QueueOfferResult.Enqueued>(await Queue!.OfferAsync(marker).WaitAsync(Deadline));
            _drained = marker.Tcs.Task;
        }

        public async Task<int> RowCount()
        {
            await using var connection = _factory.GetConnection();
            return await connection.GetTable<JournalRow>().CountAsync();
        }

        public async Task<int> TagCount()
        {
            await using var connection = _factory.GetConnection();
            return await connection.GetTable<JournalTagRow>().CountAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Shutdown.Cancel();
            Gate.Release.TrySetResult(true);
            Dao.CompleteWriteQueue();
            try
            {
                await _drained.WaitAsync(Deadline);
            }
            catch (OperationCanceledException)
            {
                _output.WriteLine("Buffered batch observed shutdown cancellation.");
            }
            Materializer.Shutdown();
            await Materializer.Supervisor.GracefulStop(Deadline);
            foreach (var write in _writes)
            {
                try
                {
                    await write.WaitAsync(Deadline);
                }
                catch (Exception e)
                {
                    _output.WriteLine($"Write result during fixture cleanup: {e.GetType().Name}");
                }
            }
            Shutdown.Dispose();
            File.Delete(_databasePath);
        }
    }

    private sealed class InsertGate : CommandInterceptor
    {
        private int _firstInsert;
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<Option<int>> ExecuteNonQueryAsync(CommandEventData eventData, DbCommand command,
            Option<int> result, CancellationToken cancellationToken)
        {
            await BeforeInsert(command);
            return result;
        }

        public override async Task<Option<object?>> ExecuteScalarAsync(CommandEventData eventData, DbCommand command,
            Option<object?> result, CancellationToken cancellationToken)
        {
            await BeforeInsert(command);
            return result;
        }

        private async Task BeforeInsert(DbCommand command)
        {
            if (command.CommandText.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase) &&
                Interlocked.CompareExchange(ref _firstInsert, 1, 0) == 0)
            {
                Entered.TrySetResult(true);
                await Release.Task;
            }
        }
    }
}
