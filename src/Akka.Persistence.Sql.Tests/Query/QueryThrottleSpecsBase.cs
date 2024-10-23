// -----------------------------------------------------------------------
//  <copyright file="QueryThrottleSpecsBase.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Dao;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Query.Dao;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK;
using Akka.Streams;
using Akka.Streams.TestKit;
using Akka.TestKit;
using Akka.TestKit.Extensions;
using Akka.Util;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

using static FluentAssertions.FluentActions;

namespace Akka.Persistence.Sql.Tests.Query;

public abstract class QueryThrottleSpecsBase<T> : PluginSpec where T : ITestContainer
{
    private TestProbe _senderProbe;
    private TestProbe _throttlerProbe;
    private ActorMaterializer _materializer;

    protected QueryThrottleSpecsBase(TagMode tagMode, ITestOutputHelper output, string name, T fixture)
        : base(FromConfig(Config(tagMode, fixture)), name, output)
    {
        // Force start read journal
        _ = Journal;

        _senderProbe = CreateTestProbe();
        _throttlerProbe = CreateTestProbe();
        _materializer = Sys.Materializer();
    }

    protected IActorRef Journal => Extension.JournalFor(null);

    protected BaseByteReadArrayJournalDao ReadJournalDao
    {
        get
        {
            var sysConfig = Sys.Settings.Config;
            var readJournalConfig = new ReadJournalConfig(sysConfig.GetConfig(SqlReadJournal.Identifier));
            var connFact = new AkkaPersistenceDataConnectionFactory(readJournalConfig);
            return new ByteArrayReadJournalDao(
                scheduler: Sys.Scheduler.Advanced,
                materializer: _materializer,
                connectionFactory: connFact,
                readJournalConfig: readJournalConfig,
                serializer: new ByteArrayJournalSerializer(
                    journalConfig: readJournalConfig,
                    serializer: Sys.Serialization,
                    separator: readJournalConfig.PluginConfig.TagSeparator,
                    writerUuid: null),
                _throttlerProbe,
                default);
        }
    }

    protected override bool SupportsSerialization => true;

    public Task DisposeAsync() => Task.CompletedTask;

    private static Configuration.Config Config(TagMode tagMode, T fixture)
    {
        if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
            throw new Exception("Failed to clean up database in 10 seconds");

        return ConfigurationFactory.ParseString(
$$"""
akka {
    loglevel = INFO
    persistence {
        journal {
            plugin = "akka.persistence.journal.sql"
            auto-start-journals = [ "akka.persistence.journal.sql" ]
            sql {
                event-adapters {
                    color-tagger  = "Akka.Persistence.TCK.Query.ColorFruitTagger, Akka.Persistence.TCK"
                }
                event-adapter-bindings = {
                    "System.String" = color-tagger
                }
                provider-name = "{{fixture.ProviderName}}"
                tag-write-mode = "{{tagMode}}"
                connection-string = "{{fixture.ConnectionString}}"
            }
        }
        query.journal.sql {
            provider-name = "{{fixture.ProviderName}}"
            connection-string = "{{fixture.ConnectionString}}"
            tag-read-mode = "{{tagMode}}"
            refresh-interval = 1s
        }
    }
}
akka.test.single-expect-default = 10s
""")
            .WithFallback(SqlPersistence.DefaultConfiguration);
    }

    protected async Task WriteMessagesAsync(int from, int to, string pid, IActorRef sender, string writerGuid)
    {
        var messages = Enumerable.Range(from, to - 1)
            .Select(i =>
                i == to - 1
                    ? new AtomicWrite(new[] {Persistent(i), Persistent(i + 1)}.ToImmutableList<IPersistentRepresentation>())
                    : new AtomicWrite(Persistent(i)));
        var probe = CreateTestProbe();

        Journal.Tell(new WriteMessages(messages, probe.Ref, ActorInstanceId));

        await probe.ExpectMsgAsync<WriteMessagesSuccessful>();
        for (var i = from; i <= to; i++)
        {
            var n = i;
            await probe.ExpectMsgAsync<WriteMessageSuccess>(m =>
                    m.Persistent.Payload.ToString() == ("a-" + n) && m.Persistent.SequenceNr == n &&
                    m.Persistent.PersistenceId == Pid);
        }

        return;

        Persistent Persistent(long i)
            => new("a-" + i, i, pid, string.Empty, false, sender, writerGuid);
    }

    [Fact(DisplayName = "BaseByteReadArrayJournalDao.AllPersistenceIdsSource should respect throttling")]
    public virtual async Task AllPersistenceIdsSourceThrottleTest()
    {
        await WriteMessagesAsync(1, 5, Pid, _senderProbe.Ref, WriterGuid);
        
        var dao = ReadJournalDao;
        var probe = dao.AllPersistenceIdsSource(long.MaxValue)
            .RunWith(this.SinkProbe<string>(), _materializer);

        await probe.ExpectSubscriptionAsync().ShouldCompleteWithin(1.Seconds());
        await probe.RequestAsync(100);
        await _throttlerProbe.ExpectMsgAsync<RequestQueryStart>();
        var streamActor = _throttlerProbe.LastSender;
        
        await probe.ExpectNoMsgAsync(200.Milliseconds());
        streamActor.Tell(QueryStartGranted.Instance);
        
        await probe.ExpectNextAsync(Pid, 1.Seconds());
        await probe.ExpectCompleteAsync();
    }

    [Fact(DisplayName = "BaseByteReadArrayJournalDao.EventsByTag should respect throttling")]
    public virtual async Task EventsByTagThrottleTest()
    {
        var a = Sys.ActorOf(Query.TestActor.Props("a"));
        var b = Sys.ActorOf(Query.TestActor.Props("b"));

        a.Tell("hello");
        await ExpectMsgAsync("hello-done");
        a.Tell("a green apple");
        await ExpectMsgAsync("a green apple-done");
        b.Tell("a black car");
        await ExpectMsgAsync("a black car-done");
        a.Tell("something else");
        await ExpectMsgAsync("something else-done");
        a.Tell("a green banana");
        await ExpectMsgAsync("a green banana-done");
        b.Tell("a green leaf");
        await ExpectMsgAsync("a green leaf-done");

        var dao = ReadJournalDao;
        var probe = dao.EventsByTag("green", 0, long.MaxValue, long.MaxValue)
            .RunWith(this.SinkProbe<Try<(IPersistentRepresentation, string[], long)>>(), _materializer);

        await probe.ExpectSubscriptionAsync().ShouldCompleteWithin(1.Seconds());
        await probe.RequestAsync(10);
        await _throttlerProbe.ExpectMsgAsync<RequestQueryStart>();
        var streamActor = _throttlerProbe.LastSender;
        
        await probe.ExpectNoMsgAsync(200.Milliseconds());
        streamActor.Tell(QueryStartGranted.Instance);

        await ValidateRepresentation(probe, "a", 2L, "a green apple", "apple", "green");
        await ValidateRepresentation(probe, "a", 4L, "a green banana", "banana", "green");
        await ValidateRepresentation(probe, "b", 2L, "a green leaf", "green");
        await probe.ExpectCompleteAsync();
        return;

        async Task ValidateRepresentation(TestSubscriber.Probe<Try<(IPersistentRepresentation, string[], long)>> p, string persistenceId, long sequenceNr, object payload, params string[] tags)
        {
            var next = await p.ExpectNextAsync(1.Seconds());
            next.IsSuccess.Should().BeTrue();
            var (representation, elemTags, _) = next.Get();
            representation.PersistenceId.Should().Be(persistenceId);
            representation.SequenceNr.Should().Be(sequenceNr);
            representation.Payload.Should().Be(payload);
            elemTags.Should().BeEquivalentTo(tags);
        }
    }

    [Fact(DisplayName = "BaseByteReadArrayJournalDao.Events should respect throttling")]
    public virtual async Task EventsThrottleTest()
    {
        await WriteMessagesAsync(1, 5, Pid, _senderProbe.Ref, WriterGuid);

        var dao = ReadJournalDao;
        var probe = dao.Events(0, long.MaxValue, long.MaxValue)
            .RunWith(this.SinkProbe<Try<(IPersistentRepresentation, string[], long)>>(), _materializer);

        await probe.ExpectSubscriptionAsync().ShouldCompleteWithin(1.Seconds());
        await probe.RequestAsync(10);
        await _throttlerProbe.ExpectMsgAsync<RequestQueryStart>();
        var streamActor = _throttlerProbe.LastSender;
        
        await probe.ExpectNoMsgAsync(200.Milliseconds());
        streamActor.Tell(QueryStartGranted.Instance);

        await ValidateRepresentation(probe, Pid, 1L, "a-1");
        await ValidateRepresentation(probe, Pid, 2L, "a-2");
        await ValidateRepresentation(probe, Pid, 3L, "a-3");
        await ValidateRepresentation(probe, Pid, 4L, "a-4");
        await ValidateRepresentation(probe, Pid, 5L, "a-5");
        await probe.ExpectCompleteAsync();
        return;

        async Task ValidateRepresentation(TestSubscriber.Probe<Try<(IPersistentRepresentation, string[], long)>> p, string persistenceId, long sequenceNr, object payload, params string[] tags)
        {
            var next = await p.ExpectNextAsync(1.Seconds());
            next.IsSuccess.Should().BeTrue();
            var (representation, elemTags, _) = next.Get();
            representation.PersistenceId.Should().Be(persistenceId);
            representation.SequenceNr.Should().Be(sequenceNr);
            representation.Payload.Should().Be(payload);
            elemTags.Should().BeEquivalentTo(tags);
        }
    }

    [Fact(DisplayName = "BaseByteReadArrayJournalDao.Messages should respect throttling")]
    public virtual async Task MessagesThrottleTest()
    {
        await WriteMessagesAsync(1, 5, Pid, _senderProbe.Ref, WriterGuid);
        
        var dao = ReadJournalDao;
        var source = await dao.Messages(Pid, 0, long.MaxValue, long.MaxValue);
        var probe = source
            .RunWith(this.SinkProbe<Try<ReplayCompletion>>(), _materializer);

        await probe.ExpectSubscriptionAsync().ShouldCompleteWithin(1.Seconds());
        await probe.RequestAsync(10);
        await _throttlerProbe.ExpectMsgAsync<RequestQueryStart>();
        var streamActor = _throttlerProbe.LastSender;
        
        await probe.ExpectNoMsgAsync(200.Milliseconds());
        streamActor.Tell(QueryStartGranted.Instance);

        await ValidateReplay(probe, Pid, 1L, "a-1");
        await ValidateReplay(probe, Pid, 2L, "a-2");
        await ValidateReplay(probe, Pid, 3L, "a-3");
        await ValidateReplay(probe, Pid, 4L, "a-4");
        await ValidateReplay(probe, Pid, 5L, "a-5");
        await probe.ExpectCompleteAsync();

        return;
        
        async Task ValidateReplay(TestSubscriber.Probe<Try<ReplayCompletion>> p, string persistenceId, long sequenceNr, object payload)
        {
            var next = await p.ExpectNextAsync();
            next.IsSuccess.Should().BeTrue();
            var completion = next.Get();
            completion.Representation.PersistenceId.Should().Be(persistenceId);
            completion.Representation.SequenceNr.Should().Be(sequenceNr);
            completion.Representation.Payload.Should().Be(payload);
        }
    }

    [Fact(DisplayName = "BaseByteReadArrayJournalDao.JournalSequence should respect throttling")]
    public virtual async Task JournalSequenceThrottleTest()
    {
        await WriteMessagesAsync(1, 5, Pid, _senderProbe.Ref, WriterGuid);
        
        var dao = ReadJournalDao;
        var probe = dao.JournalSequence(0, long.MaxValue)
            .RunWith(this.SinkProbe<long>(), _materializer);

        await probe.ExpectSubscriptionAsync().ShouldCompleteWithin(1.Seconds());
        await probe.RequestAsync(10);
        await _throttlerProbe.ExpectMsgAsync<RequestQueryStart>();
        var streamActor = _throttlerProbe.LastSender;
        
        await probe.ExpectNoMsgAsync(200.Milliseconds());
        streamActor.Tell(QueryStartGranted.Instance);

        await probe.ExpectNextAsync(1, 1.Seconds());
        await probe.ExpectNextAsync(2, 1.Seconds());
        await probe.ExpectNextAsync(3, 1.Seconds());
        await probe.ExpectNextAsync(4, 1.Seconds());
        await probe.ExpectNextAsync(5, 1.Seconds());
        await probe.ExpectCompleteAsync();
    }
    
    [Fact(DisplayName = "BaseByteReadArrayJournalDao.MaxJournalSequenceAsync should respect throttling")]
    public virtual async Task MaxJournalSequenceAsyncThrottleTest()
    {
        await WriteMessagesAsync(1, 5, Pid, _senderProbe.Ref, WriterGuid);
        
        var dao = ReadJournalDao;
        var task = dao.MaxJournalSequenceAsync();

        await _throttlerProbe.ExpectMsgAsync<RequestQueryStart>();
        var streamActor = _throttlerProbe.LastSender;

        await Awaiting(async () => await task.WaitAsync(200.Milliseconds()))
            .Should().ThrowAsync<TimeoutException>();
        streamActor.Tell(QueryStartGranted.Instance);

        (await task).Should().Be(5);
    }
    
    
}

internal class TestActor : UntypedPersistentActor
{
    public static Props Props(string persistenceId) => Actor.Props.Create(() => new TestActor(persistenceId));

    public sealed class DeleteCommand
    {
        public DeleteCommand(long toSequenceNr)
        {
            ToSequenceNr = toSequenceNr;
        }

        public long ToSequenceNr { get; }
    }

    public TestActor(string persistenceId)
    {
        PersistenceId = persistenceId;
    }

    public override string PersistenceId { get; }

    protected override void OnRecover(object message)
    {
    }

    protected override void OnCommand(object message)
    {
        switch (message)
        {
            case DeleteCommand delete:
                DeleteMessages(delete.ToSequenceNr);
                Become(WhileDeleting(Sender)); // need to wait for delete ACK to return
                break;
            case string cmd:
                var sender = Sender;
                Persist(cmd, e => sender.Tell($"{e}-done"));
                break;
        }
    }

    protected Receive WhileDeleting(IActorRef originalSender)
    {
        return message =>
        {
            switch (message)
            {
                case DeleteMessagesSuccess success:
                    originalSender.Tell($"{success.ToSequenceNr}-deleted");
                    Become(OnCommand);
                    Stash.UnstashAll();
                    break;
                case DeleteMessagesFailure failure:
                    Log.Error(failure.Cause, "Failed to delete messages to sequence number [{0}].", failure.ToSequenceNr);
                    originalSender.Tell($"{failure.ToSequenceNr}-deleted-failed");
                    Become(OnCommand);
                    Stash.UnstashAll();
                    break;
                default:
                    Stash.Stash();
                    break;
            }

            return true;
        };
    }
}
