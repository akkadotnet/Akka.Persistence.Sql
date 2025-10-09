using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence;
using Akka.Persistence.Journal;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Hosting;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Streams;
using Akka.Streams.Dsl;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Hosting.Tests;

/// <summary>
/// Tests runtime behavior of event adapters when combined with multiple journal configurations
/// (simulating the scenario where WithSqlPersistence is called with adapters, then
/// WithSqlPersistence is called again for sharding with separate journal/snapshot options)
/// </summary>
public class RuntimeEventAdapterSpec : Akka.Hosting.TestKit.TestKit, IClassFixture<SqliteContainer>
{
    private const string TestTag = "test-tag";
    private const string PersistenceId = "test-1";

    private readonly SqliteContainer _fixture;

    public RuntimeEventAdapterSpec(ITestOutputHelper output, SqliteContainer fixture)
        : base(nameof(RuntimeEventAdapterSpec), output, logLevel: LogLevel.Debug)
    {
        _fixture = fixture;
    }

    protected override async Task BeforeTestStart()
    {
        await base.BeforeTestStart();
        await _fixture.InitializeAsync();
    }

    protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
    {
        // Mimic the user's scenario from issue #552:
        // 1. First call: Set up global persistence with event adapters (for tagging)
        builder.WithSqlPersistence(
            connectionString: _fixture.ConnectionString,
            providerName: _fixture.ProviderName,
            journalBuilder: journal => journal
                .AddWriteEventAdapter<TestEventTagger>("test-tagger",
                    new[] { typeof(TestEvent) }));

        // 2. Second call: Set up separate journal/snapshot options (like sharding does)
        // This is the key issue - does this overwrite the event adapters?
        var shardingJournalOptions = new SqlJournalOptions(isDefaultPlugin: false, identifier: "sharding")
        {
            ConnectionString = _fixture.ConnectionString,
            ProviderName = _fixture.ProviderName,
            AutoInitialize = true
        };

        var shardingSnapshotOptions = new SqlSnapshotOptions(isDefaultPlugin: false, identifier: "sharding")
        {
            ConnectionString = _fixture.ConnectionString,
            ProviderName = _fixture.ProviderName,
            AutoInitialize = true
        };

        builder.WithSqlPersistence(
            journalOptions: shardingJournalOptions,
            snapshotOptions: shardingSnapshotOptions);
    }

    // Test event and adapter - mimics the user's MessageTagger
    public sealed class TestEvent
    {
        public TestEvent(string data)
        {
            Data = data;
        }

        public string Data { get; }
    }

    public sealed class TestEventTagger : IWriteEventAdapter
    {
        public string Manifest(object evt) => string.Empty;

        public object ToJournal(object evt)
        {
            return evt switch
            {
                TestEvent => new Tagged(evt, new[] { TestTag }),
                _ => evt
            };
        }
    }

    // Test persistent actor
    public sealed class TestPersistentActor : ReceivePersistentActor
    {
        public sealed class SaveEvent
        {
            public SaveEvent(string data)
            {
                Data = data;
            }

            public string Data { get; }
        }

        public sealed class GetState
        {
            public static readonly GetState Instance = new();
            private GetState() { }
        }

        public sealed class State
        {
            public State(string[] events)
            {
                Events = events;
            }

            public string[] Events { get; }
        }

        private readonly System.Collections.Generic.List<string> _events = new();

        public TestPersistentActor(string persistenceId)
        {
            PersistenceId = persistenceId;

            Command<SaveEvent>(cmd =>
            {
                var evt = new TestEvent(cmd.Data);
                Persist(evt, _ =>
                {
                    _events.Add(cmd.Data);
                    Sender.Tell("OK");
                });
            });

            Command<GetState>(_ =>
            {
                Sender.Tell(new State(_events.ToArray()));
            });

            Recover<TestEvent>(evt =>
            {
                _events.Add(evt.Data);
            });
        }

        public override string PersistenceId { get; }
    }

    [Fact]
    public async Task EventAdapter_ShouldWork_WhenFollowedByWithSqlPersistence()
    {
        // Arrange
        var persistentActor = Sys.ActorOf(Props.Create(() => new TestPersistentActor(PersistenceId)));

        // Act - persist some events
        await persistentActor.Ask<string>(new TestPersistentActor.SaveEvent("event-1"), TimeSpan.FromSeconds(5));
        await persistentActor.Ask<string>(new TestPersistentActor.SaveEvent("event-2"), TimeSpan.FromSeconds(5));
        await persistentActor.Ask<string>(new TestPersistentActor.SaveEvent("event-3"), TimeSpan.FromSeconds(5));

        // Query by tag - this should work if event adapters are configured correctly
        var readJournal = PersistenceQuery.Get(Sys)
            .ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);

        var source = readJournal.EventsByTag(TestTag, Offset.NoOffset());
        var materializer = Sys.Materializer();

        var eventsTask = source
            .Take(3)
            .RunWith(Sink.Seq<EventEnvelope>(), materializer)
            .ContinueWith(t => t.Result.ToList());

        var events = await eventsTask.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert - verify that events were tagged (meaning event adapter worked)
        events.Should().HaveCount(3, "all 3 events should be tagged");

        var eventData = events.Select(e => ((TestEvent)e.Event).Data).ToList();
        eventData.Should().Contain("event-1");
        eventData.Should().Contain("event-2");
        eventData.Should().Contain("event-3");
    }
}
