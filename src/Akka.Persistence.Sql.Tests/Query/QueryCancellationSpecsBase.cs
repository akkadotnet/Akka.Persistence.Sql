// -----------------------------------------------------------------------
//  <copyright file="QueryCancellationSpecsBase.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Dao;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using static FluentAssertions.FluentActions;

namespace Akka.Persistence.Sql.Tests.Query;

public enum CancelledQuery
{
    CurrentEventsByPersistenceId,
    EventsByTag,
    CurrentEventsByTag,
    AllEvents,
    CurrentAllEvents,
    AllEventsFromEnd,
    CurrentAllEventsFromEnd,
    EventsByTagFromEnd,
    CurrentEventsByTagFromEnd,
    CurrentPersistenceIds,
    JournalReplay,
}

/// <summary>
/// Terminating a query stream must cancel its in-flight
/// database command. The journal table is locked from another connection so the command blocks, and the
/// blocked session must go away after the stream is aborted while the lock is still held.
/// </summary>
public abstract class QueryCancellationSpecsBase<T> : PluginSpec where T : ITestContainer
{
    private const string PersistenceId = "cancel-a";

    // Long enough that a command which is not cancelled can't be confused with a command timeout.
    private const int CommandTimeoutSeconds = 60;

    private readonly T _fixture;
    private readonly ActorMaterializer _materializer;

    protected QueryCancellationSpecsBase(TagMode tagMode, ITestOutputHelper output, string name, T fixture)
        : base(FromConfig(Config(tagMode, fixture)), name, output)
    {
        _fixture = fixture;
        _ = Extension.JournalFor(null);
        _materializer = Sys.Materializer();
    }

    protected override bool SupportsSerialization => true;

    private SqlReadJournal ReadJournal => PersistenceQuery.Get(Sys).ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);

    protected abstract DbConnection CreateConnection(string connectionString);

    /// <summary>Locks the journal table until the transaction ends.</summary>
    protected abstract string LockJournalTableSql { get; }

    protected abstract string SessionIdSql { get; }

    /// <summary>Session ids blocked by the session passed as <c>@holder</c>.</summary>
    protected abstract string BlockedSessionsSql { get; }

    private static Configuration.Config Config(TagMode tagMode, T fixture)
    {
        if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
            throw new Exception("Failed to clean up database in 10 seconds");

        var connectionString = $"{fixture.ConnectionString};Command Timeout={CommandTimeoutSeconds}";

        return ConfigurationFactory.ParseString(
                $$"""
                  akka.persistence.journal {
                      plugin = "akka.persistence.journal.sql"
                      auto-start-journals = [ "akka.persistence.journal.sql" ]
                      sql {
                          event-adapters.color-tagger = "Akka.Persistence.TCK.Query.ColorFruitTagger, Akka.Persistence.TCK"
                          event-adapter-bindings."System.String" = color-tagger
                          tag-write-mode = "{{tagMode}}"
                          provider-name = "{{fixture.ProviderName}}"
                          connection-string = "{{connectionString}}"
                      }
                  }
                  akka.persistence.query.journal.sql {
                      tag-read-mode = "{{tagMode}}"
                      provider-name = "{{fixture.ProviderName}}"
                      connection-string = "{{connectionString}}"
                      refresh-interval = 200ms
                      # keep the sequence actor from polling (and blocking) while the table is locked
                      journal-sequence-retrieval.query-delay = 10m
                  }
                  """)
            .WithFallback(SqlPersistence.DefaultConfiguration);
    }

    public static IEnumerable<object[]> CancellationCases()
    {
        foreach (var query in Enum.GetValues<CancelledQuery>())
        {
            yield return new object[] { query, true };
            yield return new object[] { query, false };
        }
    }

    [Theory(DisplayName = "Stopping a query stream should cancel its in-flight database query")]
    [MemberData(nameof(CancellationCases))]
    public async Task TerminationShouldCancelInFlightQuery(CancelledQuery query, bool abort)
    {
        Sys.ActorOf(Query.TestActor.Props(PersistenceId)).Tell("a green apple");
        await ExpectMsgAsync("a green apple-done");

        // Makes sure the journal sequence actor has seen the event, so live queries reach the database.
        await ReadJournal.AllEvents(Offset.NoOffset()).Take(1).RunWith(Sink.Seq<EventEnvelope>(), _materializer);

        await using var lockConnection = CreateConnection(_fixture.ConnectionString);
        await lockConnection.OpenAsync();
        await using var lockTransaction = await lockConnection.BeginTransactionAsync();
        await using (var lockCommand = lockConnection.CreateCommand())
        {
            lockCommand.Transaction = lockTransaction;
            lockCommand.CommandText = LockJournalTableSql;
            await lockCommand.ExecuteNonQueryAsync();
            lockCommand.CommandText = SessionIdSql;
            var holder = Convert.ToInt64(await lockCommand.ExecuteScalarAsync());

            await using var monitor = CreateConnection(_fixture.ConnectionString);
            await monitor.OpenAsync();

            var (killSwitch, completion) = CreateQuery(query)
                .ViaMaterialized(KillSwitches.Single<object>(), Keep.Right)
                .ToMaterialized(Sink.Seq<object>(), Keep.Both)
                .Run(_materializer);

            var blocked = await AwaitBlockedAsync(monitor, holder, s => s.Count > 0, 10.Seconds());
            blocked.Should().NotBeEmpty("the query should be blocked by the journal table lock");

            var stopwatch = Stopwatch.StartNew();
            if (abort)
            {
                killSwitch.Abort(new OperationCanceledException("aborted by test"));
                await Awaiting(() => completion.WaitAsync(3.Seconds())).Should().ThrowAsync<OperationCanceledException>();
            }
            else
            {
                killSwitch.Shutdown();
                (await completion.WaitAsync(3.Seconds())).Should().BeEmpty();
            }

            var stillBlocked = await AwaitBlockedAsync(monitor, holder, s => !s.Intersect(blocked).Any(), 10.Seconds());
            var released = !stillBlocked.Intersect(blocked).Any();
            Output?.WriteLine(
                $"{query} ({(abort ? "Abort" : "Shutdown")}): blocked [{string.Join(", ", blocked)}], {(released ? "released" : "still blocked")} after {stopwatch.ElapsedMilliseconds} ms");
            released.Should().BeTrue(
                $"the stopped query's command should be cancelled well before the {CommandTimeoutSeconds}s command timeout");
        }

        await lockTransaction.RollbackAsync();

        var permitter = await Sys.ActorSelection($"/system/{SqlReadJournal.Identifier}-query-permitter").ResolveOne(3.Seconds());
        await AwaitAssertAsync(
            async () => (await permitter.Ask<int>(GetUsedPermits.Instance, 3.Seconds())).Should().Be(0),
            3.Seconds());

        var events = await ReadJournal.CurrentEventsByPersistenceId(PersistenceId, 0, long.MaxValue)
            .RunWith(Sink.Seq<EventEnvelope>(), _materializer)
            .WaitAsync(10.Seconds());
        events.Select(e => e.Event).Should().Equal("a green apple");
    }

    private Source<object, NotUsed> CreateQuery(CancelledQuery query)
        => query switch
        {
            CancelledQuery.CurrentEventsByPersistenceId => ReadJournal
                .CurrentEventsByPersistenceId(PersistenceId, 0, long.MaxValue).Select(e => (object)e),
            CancelledQuery.EventsByTag => ReadJournal.EventsByTag("green", Offset.NoOffset()).Select(e => (object)e),
            CancelledQuery.CurrentEventsByTag => ReadJournal.CurrentEventsByTag("green", Offset.NoOffset()).Select(e => (object)e),
            CancelledQuery.AllEvents => ReadJournal.AllEvents(Offset.NoOffset()).Select(e => (object)e),
            CancelledQuery.CurrentAllEvents => ReadJournal.CurrentAllEvents(Offset.NoOffset()).Select(e => (object)e),
            // Locking before materialization makes these cases exercise the FromEnd offset lookup.
            CancelledQuery.AllEventsFromEnd => ReadJournal.AllEvents(Offset.FromEnd(10000)).Select(e => (object)e),
            CancelledQuery.CurrentAllEventsFromEnd => ReadJournal.CurrentAllEvents(Offset.FromEnd(10000)).Select(e => (object)e),
            CancelledQuery.EventsByTagFromEnd => ReadJournal.EventsByTag("green", Offset.FromEnd(10000)).Select(e => (object)e),
            CancelledQuery.CurrentEventsByTagFromEnd => ReadJournal.CurrentEventsByTag("green", Offset.FromEnd(10000)).Select(e => (object)e),
            CancelledQuery.CurrentPersistenceIds => ReadJournal.CurrentPersistenceIds().Select(e => (object)e),
            CancelledQuery.JournalReplay => CreateJournalDao()
                .MessagesWithBatch(PersistenceId, 0, long.MaxValue, 100, Option<(TimeSpan, IScheduler)>.None)
                .Select(e => (object)e),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query, null),
        };

    private ByteArrayJournalDao CreateJournalDao()
    {
        var config = new JournalConfig(
            Sys.Settings.Config.GetConfig("akka.persistence.journal.sql").WithFallback(SqlPersistence.DefaultJournalConfiguration));
        return new ByteArrayJournalDao(
            scheduler: Sys.Scheduler.Advanced,
            mat: _materializer,
            connection: new AkkaPersistenceDataConnectionFactory(config),
            journalConfig: config,
            serializer: Sys.Serialization,
            logger: Log,
            selfUuid: null,
            shutdownToken: default);
    }

    private async Task<IReadOnlyList<long>> AwaitBlockedAsync(
        DbConnection monitor,
        long holder,
        Func<IReadOnlyList<long>, bool> condition,
        TimeSpan timeout)
    {
        IReadOnlyList<long> sessions = Array.Empty<long>();
        await AwaitAssertAsync(async () =>
        {
            await using var command = monitor.CreateCommand();
            command.CommandText = BlockedSessionsSql;
            command.CommandTimeout = (int)Math.Ceiling(timeout.TotalSeconds);
            var parameter = command.CreateParameter();
            parameter.ParameterName = "holder";
            parameter.Value = holder;
            command.Parameters.Add(parameter);

            var current = new List<long>();
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    current.Add(Convert.ToInt64(reader.GetValue(0)));
            }

            sessions = current;
            condition(sessions).Should().BeTrue("the expected database blocking state should be observed");
        }, timeout, 50.Milliseconds());
        return sessions;
    }
}
