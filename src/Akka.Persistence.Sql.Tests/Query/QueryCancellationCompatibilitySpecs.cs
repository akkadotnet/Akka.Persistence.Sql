// -----------------------------------------------------------------------
//  <copyright file="QueryCancellationCompatibilitySpecs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2026 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Dao;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Utility;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.TestKit;
using Akka.Util;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using static FluentAssertions.FluentActions;

namespace Akka.Persistence.Sql.Tests.Query;

public class QueryCancellationCompatibilitySpecs : Akka.TestKit.Xunit.TestKit
{
    public QueryCancellationCompatibilitySpecs(ITestOutputHelper output) : base("{}", output)
    {
    }

    [Fact]
    public async Task BatchedReplayShouldContinueCallingExistingCustomDaoOverride()
    {
        var dao = CreateDao((id, from, _, max) => Task.FromResult(
            Source.From(Enumerable.Range((int)from, (int)Math.Min(max, 4 - from))
                .Select(sequence => Replay(id, sequence)))));

        var messages = await dao.MessagesWithBatch("custom", 0, 3, 2, Option<(TimeSpan, IScheduler)>.None)
            .RunWith(Sink.Seq<Try<ReplayCompletion>>(), Sys.Materializer())
            .WaitAsync(3.Seconds());

        messages.Select(message => message.Get().Representation.SequenceNr).Should().Equal(1, 2, 3);
        dao.Calls.Should().Equal(("custom", 1L, 3L, 2L), ("custom", 3L, 3L, 2L));
    }

    [Fact]
    public async Task BatchedReplayShouldCancelMaterializedCustomDaoSource()
    {
        var materialized = new TaskCompletionSource<Task<Done>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dao = CreateDao((_, _, _, _) => Task.FromResult(
            Source.Maybe<Try<ReplayCompletion>>().WatchTermination((_, completion) =>
            {
                materialized.TrySetResult(completion);
                return NotUsed.Instance;
            })));

        var (killSwitch, completion) = dao.MessagesWithBatch("custom", 0, long.MaxValue, 2, Option<(TimeSpan, IScheduler)>.None)
            .ViaMaterialized(KillSwitches.Single<Try<ReplayCompletion>>(), Keep.Right)
            .ToMaterialized(Sink.Ignore<Try<ReplayCompletion>>(), Keep.Both)
            .Run(Sys.Materializer());
        var innerCompletion = await materialized.Task.WaitAsync(3.Seconds());

        killSwitch.Shutdown();
        await completion.WaitAsync(3.Seconds());
        await innerCompletion.WaitAsync(3.Seconds());
        dao.Calls.Should().ContainSingle();
    }

    [Fact]
    public async Task BatchedReplayShouldUseSuppliedRefreshScheduler()
    {
        var scheduler = new TestScheduler(Sys.Settings.Config, Log);
        var invocation = 0;
        var dao = CreateDao((id, _, _, _) => Task.FromResult(
            Interlocked.Increment(ref invocation) == 1
                ? Source.Empty<Try<ReplayCompletion>>()
                : Source.Single(Replay(id, 1))));
        var completion = dao.MessagesWithBatch("custom", 0, 1, 2,
                Option<(TimeSpan, IScheduler)>.Create((1.Hours(), scheduler)))
            .RunWith(Sink.Seq<Try<ReplayCompletion>>(), Sys.Materializer());

        // Advancing the supplied scheduler must finish a one-hour refresh without waiting real time.
        await AwaitAssertAsync(() =>
        {
            scheduler.Advance(1.Hours());
            completion.IsCompleted.Should().BeTrue();
        }, 3.Seconds());
        var messages = await completion;
        messages.Single().Get().Representation.SequenceNr.Should().Be(1);
        dao.Calls.Should().HaveCount(2);
    }

    [Fact]
    public async Task RefreshDelayShouldCompleteWhenSuppliedSchedulerAdvances()
    {
        var scheduler = new TestScheduler(Sys.Settings.Config, Log);
        var delay = QueryDelay.WaitAsync(1.Hours(), scheduler, CancellationToken.None);

        delay.IsCompleted.Should().BeFalse();
        scheduler.Advance(59.Minutes());
        delay.IsCompleted.Should().BeFalse();
        scheduler.Advance(1.Minutes());
        await delay.WaitAsync(3.Seconds());
    }

    [Fact]
    public async Task RefreshDelayShouldCancelWithoutWaitingForScheduler()
    {
        var scheduler = new TestScheduler(Sys.Settings.Config, Log);
        using var cancellation = new CancellationTokenSource();
        var delay = QueryDelay.WaitAsync(1.Hours(), scheduler, cancellation.Token);

        cancellation.Cancel();
        await Awaiting(() => delay.WaitAsync(3.Seconds())).Should().ThrowAsync<OperationCanceledException>();
        scheduler.Advance(2.Hours());
        delay.IsCanceled.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshDelayShouldHonorAlreadyCancelledToken()
    {
        var scheduler = new TestScheduler(Sys.Settings.Config, Log);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Awaiting(() => QueryDelay.WaitAsync(1.Hours(), scheduler, cancellation.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    private CustomJournalDao CreateDao(Func<string, long, long, long, Task<Source<Try<ReplayCompletion>, NotUsed>>> messages)
    {
        var config = new JournalConfig(ConfigurationFactory.ParseString("""
            provider-name = "SQLite.MS"
            connection-string = "Data Source=:memory:"
            """).WithFallback(SqlPersistence.DefaultJournalConfiguration));
        return new CustomJournalDao(Sys.Scheduler.Advanced, Sys.Materializer(),
            new AkkaPersistenceDataConnectionFactory(config), config, messages);
    }

    private static Try<ReplayCompletion> Replay(string persistenceId, long sequence)
        => new(new ReplayCompletion(
            new Persistent(sequence, sequence, persistenceId, string.Empty, false, ActorRefs.NoSender, string.Empty),
            Array.Empty<string>(), sequence));

    private sealed class CustomJournalDao : BaseJournalDaoWithReadMessages
    {
        private readonly Func<string, long, long, long, Task<Source<Try<ReplayCompletion>, NotUsed>>> _messages;
        public ConcurrentQueue<(string PersistenceId, long From, long To, long Max)> Calls { get; } = new();

        public CustomJournalDao(IAdvancedScheduler scheduler, IMaterializer materializer,
            AkkaPersistenceDataConnectionFactory connectionFactory, JournalConfig config,
            Func<string, long, long, long, Task<Source<Try<ReplayCompletion>, NotUsed>>> messages)
            : base(scheduler, materializer, connectionFactory, config, CancellationToken.None)
        {
            _messages = messages;
        }

        public override Task<Source<Try<ReplayCompletion>, NotUsed>> Messages(
            string persistenceId, long fromSequenceNr, long toSequenceNr, long max)
        {
            Calls.Enqueue((persistenceId, fromSequenceNr, toSequenceNr, max));
            return _messages(persistenceId, fromSequenceNr, toSequenceNr, max);
        }
    }
}
