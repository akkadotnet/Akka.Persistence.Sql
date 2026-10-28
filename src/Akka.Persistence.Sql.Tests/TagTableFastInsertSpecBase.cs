using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Persistence.Journal;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Dao;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Streams;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Interceptors;
using Xunit;

namespace Akka.Persistence.Sql.Tests;

/// <summary>
/// Correctness checks for the tag-table AsQueryable insert path: large atomic writes are split
/// into commands that respect the provider's parameter limit, every command runs in the same
/// transaction, and tag rows are correlated to the right journal ordering.
/// </summary>
public abstract class TagTableFastInsertSpecBase : Akka.TestKit.Xunit.TestKit
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);

    protected TagTableFastInsertSpecBase(ITestOutputHelper output)
        : base(SqlPersistence.DefaultConfiguration.ToString(), output)
    {
    }

    /// <summary>LinqToDB's BulkCopy parameter limit for the provider under test.</summary>
    protected abstract int ProviderMaxParameters { get; }

    /// <summary>
    /// A parameter limit above <see cref="ProviderMaxParameters"/> that the database under test accepts,
    /// or <c>null</c> when there is no headroom (e.g. SQL Server's hard limit of 2100).
    /// </summary>
    protected virtual int? RaisedMaxParameters => null;

    protected abstract string ConnectionString(string databaseId);

    protected abstract string ProviderName { get; }

    protected virtual void Cleanup(string databaseId)
    {
    }

    public enum WriteSize
    {
        ExactlyOneCommand,
        OneRowOverOneCommand,
        Thousand,
    }

    [Theory]
    [InlineData(TagTableQueryableInsertMode.Parameterized, true, WriteSize.ExactlyOneCommand)]
    [InlineData(TagTableQueryableInsertMode.Parameterized, true, WriteSize.OneRowOverOneCommand)]
    [InlineData(TagTableQueryableInsertMode.Parameterized, true, WriteSize.Thousand)]
    [InlineData(TagTableQueryableInsertMode.Parameterized, false, WriteSize.ExactlyOneCommand)]
    [InlineData(TagTableQueryableInsertMode.Parameterized, false, WriteSize.Thousand)]
    [InlineData(TagTableQueryableInsertMode.Inline, true, WriteSize.ExactlyOneCommand)]
    [InlineData(TagTableQueryableInsertMode.Inline, true, WriteSize.OneRowOverOneCommand)]
    [InlineData(TagTableQueryableInsertMode.Inline, true, WriteSize.Thousand)]
    [InlineData(TagTableQueryableInsertMode.Inline, false, WriteSize.ExactlyOneCommand)]
    [InlineData(TagTableQueryableInsertMode.Inline, false, WriteSize.Thousand)]
    public async Task Large_atomic_write_is_split_by_provider_parameter_limit(
        TagTableQueryableInsertMode mode,
        bool writerUuid,
        WriteSize size)
    {
        var parametersPerRow = BaseByteArrayJournalDao.JournalInsertParametersPerRow(mode, writerUuid);
        var rowsPerCommand = ProviderMaxParameters / parametersPerRow;
        var events = size switch
        {
            WriteSize.ExactlyOneCommand => rowsPerCommand,
            WriteSize.OneRowOverOneCommand => rowsPerCommand + 1,
            _ => 1000,
        };
        await using var fixture = await Fixture.Create(this, mode, writerUuid);

        var result = await fixture.WriteAtomic("pid", 1, events).WaitAsync(Deadline);

        Assert.Null(result);
        Assert.Equal((events + rowsPerCommand - 1) / rowsPerCommand, fixture.Counter.JournalInserts);
        // The per-row estimate must match what LinqToDB actually sends, not just stay under it.
        Assert.Equal(events * parametersPerRow, fixture.Counter.JournalParameters.Sum());
        Assert.All(fixture.Counter.JournalParameters, p => Assert.InRange(p, 1, ProviderMaxParameters));
        await AssertRowsAndTagsCorrelate(fixture, events);
    }

    [Theory]
    [InlineData(TagTableQueryableInsertMode.Parameterized)]
    [InlineData(TagTableQueryableInsertMode.Inline)]
    public async Task Configured_max_parameters_lowers_rows_per_command(TagTableQueryableInsertMode mode)
    {
        const int maxParameters = 70;
        var rowsPerCommand = maxParameters / BaseByteArrayJournalDao.JournalInsertParametersPerRow(mode, true);
        await using var fixture = await Fixture.Create(this, mode, writerUuid: true, maxParameters: maxParameters);

        var result = await fixture.WriteAtomic("pid", 1, 95).WaitAsync(Deadline);

        Assert.Null(result);
        Assert.Equal((95 + rowsPerCommand - 1) / rowsPerCommand, fixture.Counter.JournalInserts);
        Assert.All(fixture.Counter.JournalParameters, p => Assert.InRange(p, 1, maxParameters));
        await AssertRowsAndTagsCorrelate(fixture, 95);
    }

    [Theory]
    [InlineData(TagTableQueryableInsertMode.Parameterized)]
    [InlineData(TagTableQueryableInsertMode.Inline)]
    public async Task Configured_max_parameters_can_raise_the_provider_limit(TagTableQueryableInsertMode mode)
    {
        Assert.SkipUnless(RaisedMaxParameters is not null, "The database has no headroom above the LinqToDB limit.");
        var maxParameters = RaisedMaxParameters!.Value;
        var parametersPerRow = BaseByteArrayJournalDao.JournalInsertParametersPerRow(mode, true);
        // More rows than one command could hold under the LinqToDB limit, but fewer than the raised one.
        var events = ProviderMaxParameters / parametersPerRow * 2;
        Assert.True(events * parametersPerRow <= maxParameters);
        await using var fixture = await Fixture.Create(
            this, mode, writerUuid: true, maxParameters: maxParameters, preferParametersOnMultiRowInsert: true);

        var result = await fixture.WriteAtomic("pid", 1, events).WaitAsync(Deadline);

        Assert.Null(result);
        Assert.Equal(1, fixture.Counter.JournalInserts);
        Assert.Equal(events * parametersPerRow, fixture.Counter.JournalParameters.Single());
        // LinqToDB 6.5.0+ lets MaxParametersForBatch raise its own limit for the tag-table BulkCopy too.
        Assert.InRange(fixture.Counter.TagParameters.Max(), ProviderMaxParameters + 1, maxParameters);
        await AssertRowsAndTagsCorrelate(fixture, events);
    }

    [Theory]
    [InlineData(TagTableQueryableInsertMode.Parameterized)]
    [InlineData(TagTableQueryableInsertMode.Inline)]
    public async Task Configured_max_parameters_also_limits_parameterized_tag_inserts(TagTableQueryableInsertMode mode)
    {
        // 4 parameters per tag row -> at most 17 tag rows per command with a limit of 70.
        const int maxParameters = 70;
        await using var fixture = await Fixture.Create(
            this, mode, writerUuid: true, maxParameters: maxParameters, preferParametersOnMultiRowInsert: true);

        var result = await fixture.WriteAtomic("pid", 1, 95).WaitAsync(Deadline);

        Assert.Null(result);
        Assert.NotEmpty(fixture.Counter.TagParameters);
        Assert.All(fixture.Counter.TagParameters, p => Assert.InRange(p, 1, maxParameters));
        await AssertRowsAndTagsCorrelate(fixture, 95);
    }

    [Theory]
    [InlineData(TagTableQueryableInsertMode.Parameterized)]
    [InlineData(TagTableQueryableInsertMode.Inline)]
    public async Task Failure_in_a_later_command_rolls_back_the_whole_atomic_write(TagTableQueryableInsertMode mode)
    {
        // The conflicting row is the first row of the third command.
        var rowsPerCommand = ProviderMaxParameters / BaseByteArrayJournalDao.JournalInsertParametersPerRow(mode, true);
        var conflictingSequenceNr = rowsPerCommand * 2L + 1;
        await using var fixture = await Fixture.Create(this, mode, writerUuid: true);
        Assert.Null(await fixture.WriteAtomic("pid", conflictingSequenceNr, 1).WaitAsync(Deadline));
        fixture.Counter.Reset();

        await Assert.ThrowsAnyAsync<DbException>(
            () => fixture.WriteAtomic("pid", 1, rowsPerCommand * 3).WaitAsync(Deadline));

        // Every command before the one holding the conflicting row ran; none of their rows may survive.
        // Some providers (SQL Server) open the failing command's OUTPUT reader before raising the error,
        // so it may be counted as well.
        var completedBeforeConflict = (int)((conflictingSequenceNr - 1) / rowsPerCommand);
        Assert.True(completedBeforeConflict > 0);
        Assert.InRange(fixture.Counter.JournalInserts, completedBeforeConflict, completedBeforeConflict + 1);
        await AssertRowsAndTagsCorrelate(fixture, expectedRows: 1);
    }

    private static async Task AssertRowsAndTagsCorrelate(Fixture fixture, int expectedRows)
    {
        await using var connection = fixture.Factory.GetConnection();
        var rows = await connection.GetTable<JournalRow>().ToListAsync();
        var tags = await connection.GetTable<JournalTagRow>().ToListAsync();

        Assert.Equal(expectedRows, rows.Count);
        Assert.Equal(rows.Sum(r => ExpectedTags(r.SequenceNumber).Length), tags.Count);

        var ordering = rows.ToDictionary(r => (r.PersistenceId, r.SequenceNumber), r => r.Ordering);
        foreach (var tag in tags)
        {
            Assert.Equal(ordering[(tag.PersistenceId, tag.SequenceNumber)], tag.OrderingId);
            Assert.Contains(tag.TagValue, ExpectedTags(tag.SequenceNumber));
        }
    }

    // Every third event is untagged; the rest carry two tags derived from the sequence number.
    private static string[] ExpectedTags(long sequenceNr)
        => sequenceNr % 3 == 0 ? Array.Empty<string>() : new[] { "all", $"seq-{sequenceNr % 7}" };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly TagTableFastInsertSpecBase _spec;
        private readonly string _databaseId = Guid.NewGuid().ToString("N");
        private readonly CancellationTokenSource _shutdown = new();
        private readonly ActorMaterializer _materializer;

        private Fixture(
            TagTableFastInsertSpecBase spec,
            TagTableQueryableInsertMode mode,
            bool writerUuid,
            int maxParameters,
            bool preferParametersOnMultiRowInsert)
        {
            _spec = spec;
            var journalTable = $"fi_{_databaseId}";
            var tagTable = $"fi_{_databaseId}_tags";
            Counter = new JournalInsertCounter(journalTable, tagTable);
            var config = ConfigurationFactory.ParseString($$"""
                provider-name = "{{spec.ProviderName}}"
                connection-string = "{{spec.ConnectionString(_databaseId)}}"
                tag-write-mode = TagTable
                tagtable-asqueryable-insert-mode = {{mode.ToString().ToLowerInvariant()}}
                tagtable-asqueryable-insert-max-parameters = {{maxParameters}}
                prefer-parameters-on-multirow-insert = {{(preferParametersOnMultiRowInsert ? "true" : "false")}}
                use-clone-connection = false
                default.journal {
                  table-name = "{{journalTable}}"
                  use-writer-uuid-column = {{(writerUuid ? "true" : "false")}}
                }
                default.metadata.table-name = "{{journalTable}}_metadata"
                default.tag.table-name = "{{tagTable}}"
                """).WithFallback(SqlPersistence.DefaultJournalConfiguration);
            var journalConfig = new JournalConfig(config);
            var options = new DataOptions()
                .UseConnectionString(journalConfig.ProviderName, journalConfig.ConnectionString)
                .UseInterceptor(Counter);
            Factory = new AkkaPersistenceDataConnectionFactory(journalConfig.WithDataOptions(options));
            _materializer = ActorMaterializer.Create(spec.Sys, ActorMaterializerSettings.Create(spec.Sys));
            Dao = new ByteArrayJournalDao(spec.Sys.Scheduler.Advanced, _materializer, Factory, journalConfig,
                spec.Sys.Serialization, Logging.GetLogger(spec.Sys, nameof(Fixture)), "fast-insert-test", _shutdown.Token);
        }

        public static async Task<Fixture> Create(
            TagTableFastInsertSpecBase spec,
            TagTableQueryableInsertMode mode,
            bool writerUuid,
            int maxParameters = 0,
            bool preferParametersOnMultiRowInsert = false)
        {
            var fixture = new Fixture(spec, mode, writerUuid, maxParameters, preferParametersOnMultiRowInsert);
            await fixture.Dao.InitializeTables(CancellationToken.None);
            fixture.Counter.Reset();
            return fixture;
        }

        public AkkaPersistenceDataConnectionFactory Factory { get; }
        public ByteArrayJournalDao Dao { get; }
        public JournalInsertCounter Counter { get; }

        public async Task<Exception?> WriteAtomic(string persistenceId, long fromSequenceNr, int count)
        {
            var events = Enumerable.Range(0, count)
                .Select(i => fromSequenceNr + i)
                .Select(seq =>
                {
                    var tags = ExpectedTags(seq);
                    object payload = tags.Length == 0
                        ? new byte[] { 1, 2, 3 }
                        : new Tagged(new byte[] { 1, 2, 3 }, tags.ToImmutableHashSet());
                    return (IPersistentRepresentation)new Persistent(payload, seq, persistenceId, writerGuid: "writer");
                })
                .ToImmutableList();

            var results = await Dao.AsyncWriteMessages(new[] { new AtomicWrite(events) }, CancellationToken.None);
            return results.Single();
        }

        public async ValueTask DisposeAsync()
        {
            Dao.CompleteWriteQueue();
            _shutdown.Cancel();
            _materializer.Shutdown();
            await _materializer.Supervisor.GracefulStop(TimeSpan.FromSeconds(5));
            _shutdown.Dispose();
            _spec.Cleanup(_databaseId);
        }
    }

    private sealed class JournalInsertCounter : CommandInterceptor
    {
        private readonly string _journalTable;
        private readonly string _tagTable;
        private readonly ConcurrentQueue<int> _journalParameters = new();
        private readonly ConcurrentQueue<int> _tagParameters = new();
        private int _journalInserts;

        public JournalInsertCounter(string journalTable, string tagTable)
        {
            _journalTable = journalTable;
            _tagTable = tagTable;
        }

        /// <summary>Journal INSERT commands that executed (opened their OUTPUT/RETURNING reader).</summary>
        public int JournalInserts => Volatile.Read(ref _journalInserts);

        /// <summary>Parameter count of every journal INSERT command sent.</summary>
        public int[] JournalParameters => _journalParameters.ToArray();

        /// <summary>Parameter count of every tag-table INSERT command sent.</summary>
        public int[] TagParameters => _tagParameters.ToArray();

        public void Reset()
        {
            Interlocked.Exchange(ref _journalInserts, 0);
            _journalParameters.Clear();
            _tagParameters.Clear();
        }

        public override DbCommand CommandInitialized(CommandEventData eventData, DbCommand command)
        {
            if (IsInsertInto(command, _tagTable))
                _tagParameters.Enqueue(command.Parameters.Count);
            else if (IsInsertInto(command, _journalTable))
                _journalParameters.Enqueue(command.Parameters.Count);
            return command;
        }

        // The AsQueryable path uses INSERT ... OUTPUT/RETURNING, which runs as a reader.
        public override void AfterExecuteReader(CommandEventData eventData, DbCommand command,
            CommandBehavior commandBehavior, DbDataReader dataReader)
        {
            if (!IsInsertInto(command, _tagTable) && IsInsertInto(command, _journalTable))
                Interlocked.Increment(ref _journalInserts);
        }

        private static bool IsInsertInto(DbCommand command, string table)
            => command.CommandText.Contains("INSERT INTO", StringComparison.OrdinalIgnoreCase) &&
               command.CommandText.Contains(table, StringComparison.OrdinalIgnoreCase);
    }
}
