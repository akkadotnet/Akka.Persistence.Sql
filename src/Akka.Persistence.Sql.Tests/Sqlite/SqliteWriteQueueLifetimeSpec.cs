using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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

public class SqliteWriteQueueLifetimeSpec : Akka.TestKit.Xunit.TestKit
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);
    private readonly ITestOutputHelper _output;

    public SqliteWriteQueueLifetimeSpec(ITestOutputHelper output)
        : base(SqlPersistence.DefaultConfiguration.ToString(), output) => _output = output;

    [Theory]
    [InlineData(TagMode.Csv)]
    [InlineData(TagMode.TagTable)]
    public async Task Shutdown_fails_queued_writes_and_closes_admission(TagMode mode)
    {
        await using var fixture = await CreateFixture(mode);
        var active = fixture.Write("active");
        await fixture.Gate.Entered.Task.WaitAsync(Deadline);
        var queued = fixture.Write("queued");
        await fixture.AdmissionBarrier();

        fixture.Shutdown.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued.WaitAsync(Deadline));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Write("later").WaitAsync(Deadline));
        Assert.False(active.IsCompleted); // The database operation still owns its outcome.

        fixture.Gate.Release.TrySetResult(true);
        var activeFailure = await Assert.ThrowsAnyAsync<Exception>(() => active.WaitAsync(Deadline));
        var failures = activeFailure is AggregateException aggregate
            ? aggregate.Flatten().InnerExceptions.ToArray()
            : new[] { activeFailure };
        Assert.Contains(failures, failure => failure is OperationCanceledException);
        Assert.Equal(0, await fixture.RowCount());
    }

    [Theory]
    [InlineData(TagMode.Csv)]
    [InlineData(TagMode.TagTable)]
    public async Task Stream_failure_fails_queued_writes_without_overwriting_an_active_commit(TagMode mode)
    {
        await using var fixture = await CreateFixture(mode);
        var active = fixture.Write("active");
        await fixture.Gate.Entered.Task.WaitAsync(Deadline);
        var queued = fixture.Write("queued");
        await fixture.AdmissionBarrier();

        fixture.Queue.Fail(new InvalidOperationException("Injected writer stream failure"));

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => queued.WaitAsync(Deadline));
        Assert.IsNotType<TimeoutException>(failure);
        Assert.Contains("Injected writer stream failure", failure.ToString());
        Assert.False(active.IsCompleted);
        var rejected = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Write("later").WaitAsync(Deadline));
        Assert.IsNotType<TimeoutException>(rejected);

        fixture.Gate.Release.TrySetResult(true);
        await active.WaitAsync(Deadline);
        Assert.Equal(1, await fixture.RowCount());
        if (mode == TagMode.TagTable)
            Assert.Equal(2, await fixture.TagCount());
    }

    [Theory]
    [InlineData(TagMode.Csv)]
    [InlineData(TagMode.TagTable)]
    public async Task Completing_the_queue_drains_accepted_writes(TagMode mode)
    {
        await using var fixture = await CreateFixture(mode);
        var active = fixture.Write("active");
        await fixture.Gate.Entered.Task.WaitAsync(Deadline);
        var queued = fixture.Write("queued");
        await fixture.AdmissionBarrier();

        fixture.Queue.Complete();
        fixture.Gate.Release.TrySetResult(true);

        await Task.WhenAll(active, queued).WaitAsync(Deadline);
        Assert.Equal(2, await fixture.RowCount());
        if (mode == TagMode.TagTable)
            Assert.Equal(4, await fixture.TagCount());
        var rejected = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Write("later").WaitAsync(Deadline));
        Assert.IsNotType<TimeoutException>(rejected);
    }

    [Fact]
    public async Task Materializer_termination_settles_queued_writes_with_the_actor_system_alive()
    {
        await using var fixture = await CreateFixture(TagMode.Csv);
        var active = fixture.Write("active");
        await fixture.Gate.Entered.Task.WaitAsync(Deadline);
        var queued = fixture.Write("queued");
        await fixture.AdmissionBarrier();

        fixture.Materializer.Shutdown();
        await fixture.Materializer.Supervisor.GracefulStop(Deadline);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => queued.WaitAsync(Deadline));
        Assert.IsNotType<TimeoutException>(failure);
        Assert.False(Sys.WhenTerminated.IsCompleted);
        Assert.False(active.IsCompleted);
        fixture.Gate.Release.TrySetResult(true);
        await active.WaitAsync(Deadline);
        Assert.Equal(1, await fixture.RowCount());
    }

    [Fact]
    public async Task An_already_canceled_request_does_not_enter_the_queue()
    {
        await using var fixture = await CreateFixture(TagMode.Csv);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Write("canceled", canceled.Token).WaitAsync(Deadline));
        Assert.False(fixture.Gate.Entered.Task.IsCompleted);
        fixture.Gate.Release.TrySetResult(true);
        await fixture.Write("healthy").WaitAsync(Deadline);
        Assert.Equal(1, await fixture.RowCount());
    }

    [Fact]
    public async Task A_queue_constructed_during_shutdown_rejects_writes()
    {
        await using var fixture = await CreateFixture(TagMode.Csv, alreadyStopped: true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Write("later").WaitAsync(Deadline));
        Assert.False(fixture.Gate.Entered.Task.IsCompleted);
    }

    private async Task<QueueFixture> CreateFixture(TagMode mode, bool alreadyStopped = false)
    {
        var fixture = new QueueFixture(Sys, mode, alreadyStopped, _output);
        await fixture.Dao.InitializeTables(CancellationToken.None);
        return fixture;
    }

    private sealed class QueueFixture : IAsyncDisposable
    {
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"sql-write-queue-{Guid.NewGuid():N}.db");
        private readonly AkkaPersistenceDataConnectionFactory _factory;
        private readonly List<Task> _writes = new();
        private readonly ITestOutputHelper _output;

        public QueueFixture(ActorSystem system, TagMode mode, bool alreadyStopped, ITestOutputHelper output)
        {
            _output = output;
            if (alreadyStopped)
                Shutdown.Cancel();
            var config = ConfigurationFactory.ParseString($$"""
                provider-name = SQLite.MS
                connection-string = "Data Source={{_databasePath.Replace('\\', '/')}};Pooling=False"
                tag-write-mode = {{mode}}
                use-clone-connection = false
                parallelism = 1
                batch-size = 1
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
            Assert.NotNull(queueField);
            Queue = Assert.IsAssignableFrom<ISourceQueueWithComplete<WriteQueueEntry>>(queueField.GetValue(Dao));
        }

        public ByteArrayJournalDao Dao { get; }
        public ISourceQueueWithComplete<WriteQueueEntry> Queue { get; }
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
            var marker = new WriteQueueEntry(new TaskCompletionSource<NotUsed>(TaskCreationOptions.RunContinuationsAsynchronously),
                LanguageExt.Seq<JournalRow>.Empty, CancellationToken.None);
            Assert.IsType<QueueOfferResult.Enqueued>(await Queue.OfferAsync(marker).WaitAsync(Deadline));
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
