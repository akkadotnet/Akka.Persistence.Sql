using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence;
using Akka.Persistence.Hosting;
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
        // 1. First call: Set up global persistence with event adapters (for tagging) using NEW callback API
        builder.WithSqlPersistence(
            connectionString: _fixture.ConnectionString,
            providerName: _fixture.ProviderName,
            autoInitialize: true,
            journalBuilder: journal => journal.AddWriteEventAdapter<TestEventTagger>("test-tagger", new[] { typeof(TestEvent) }));

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

        builder.WithJournalAndSnapshot(
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
        public static int CallCount = 0;

        public string Manifest(object evt) => string.Empty;

        public object ToJournal(object evt)
        {
            var result = evt switch
            {
                TestEvent => new Tagged(evt, new[] { TestTag }),
                _ => evt
            };

            System.Threading.Interlocked.Increment(ref CallCount);
            System.Console.WriteLine($"[TestEventTagger] ToJournal called {CallCount} times. Event: {evt.GetType().Name}, Result: {result.GetType().Name}");

            return result;
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

        public TestPersistentActor(string persistenceId, string? journalPluginId = null)
        {
            PersistenceId = persistenceId;
            if (journalPluginId != null)
                JournalPluginId = journalPluginId;

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
    public async Task EventAdapter_ShouldWork_OnDefaultJournal()
    {
        // This test verifies adapters work on the default journal
        TestEventTagger.CallCount = 0; // Reset counter

        // Arrange - use default journal
        var persistentActor = Sys.ActorOf(Props.Create(() => new TestPersistentActor(PersistenceId, null)));

        // Act - persist some events
        Output.WriteLine("Persisting event-1...");
        await persistentActor.Ask<string>(new TestPersistentActor.SaveEvent("event-1"), TimeSpan.FromSeconds(5));
        Output.WriteLine("Persisting event-2...");
        await persistentActor.Ask<string>(new TestPersistentActor.SaveEvent("event-2"), TimeSpan.FromSeconds(5));
        Output.WriteLine("Persisting event-3...");
        await persistentActor.Ask<string>(new TestPersistentActor.SaveEvent("event-3"), TimeSpan.FromSeconds(5));
        Output.WriteLine("All events persisted successfully");

        // Give a moment for async writes to complete
        await Task.Delay(1000);

        Output.WriteLine($"Event adapter was called {TestEventTagger.CallCount} times");

        // Query by tag - this should work if event adapters are configured correctly
        Output.WriteLine($"Querying for events with tag: {TestTag}");
        var readJournal = PersistenceQuery.Get(Sys)
            .ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);

        var source = readJournal.EventsByTag(TestTag, Offset.NoOffset());
        var materializer = Sys.Materializer();

        Output.WriteLine("Starting stream materialization...");
        var eventsTask = source
            .Take(3)
            .RunWith(Sink.Seq<EventEnvelope>(), materializer)
            .ContinueWith(t => {
                Output.WriteLine($"Stream completed with {t.Result.Count} events");
                return t.Result.ToList();
            });

        var events = await eventsTask.WaitAsync(TimeSpan.FromSeconds(10));
        Output.WriteLine($"Received {events.Count} events from query");

        // Assert - verify that events were tagged (meaning event adapter worked)
        events.Should().HaveCount(3, "all 3 events should be tagged");

        var eventData = events.Select(e => ((TestEvent)e.Event).Data).ToList();
        eventData.Should().Contain("event-1");
        eventData.Should().Contain("event-2");
        eventData.Should().Contain("event-3");
    }

    [Fact]
    public async Task EventAdapter_ShouldWork_OnShardingJournal_ReproducesUserScenario()
    {
        // This test reproduces the user's actual scenario from issue #552:
        // Adapters configured on default journal, but sharded entities use "sharding" journal
        TestEventTagger.CallCount = 0; // Reset counter

        // Arrange - use "sharding" journal (like sharded entities do)
        var persistentActor = Sys.ActorOf(Props.Create(() => new TestPersistentActor($"{PersistenceId}-sharding", "akka.persistence.journal.sharding")));

        // Act - persist some events
        Output.WriteLine("Persisting to sharding journal...");
        await persistentActor.Ask<string>(new TestPersistentActor.SaveEvent("event-1"), TimeSpan.FromSeconds(5));
        await persistentActor.Ask<string>(new TestPersistentActor.SaveEvent("event-2"), TimeSpan.FromSeconds(5));
        await persistentActor.Ask<string>(new TestPersistentActor.SaveEvent("event-3"), TimeSpan.FromSeconds(5));
        Output.WriteLine("All events persisted successfully");

        // Wait for events to be fully written and available for querying
        await Task.Delay(1000);

        Output.WriteLine($"Event adapter was called {TestEventTagger.CallCount} times");
        Output.WriteLine("EXPECTED: 0 times (bug reproduction - adapters not on sharding journal)");

        // Query by tag - this should FAIL because adapters aren't on the sharding journal
        var readJournal = PersistenceQuery.Get(Sys)
            .ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);

        // Use CurrentEventsByTag (point-in-time) to avoid picking up tagged events
        // from the other test method that shares the same database
        var materializer = Sys.Materializer();
        var taggedEvents = await readJournal.CurrentEventsByTag(TestTag, Offset.NoOffset())
            .RunWith(Sink.Seq<EventEnvelope>(), materializer);

        // Filter to only events from the sharding persistence ID
        var shardingTaggedEvents = taggedEvents
            .Where(e => e.PersistenceId == $"{PersistenceId}-sharding")
            .ToList();

        // Assert - events persisted through sharding journal should NOT be tagged
        shardingTaggedEvents.Should().BeEmpty("events should not be tagged when using sharding journal without adapters");
        TestEventTagger.CallCount.Should().Be(0, "adapter should not be called for sharding journal");
    }
}
