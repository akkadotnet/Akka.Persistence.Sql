// -----------------------------------------------------------------------
//  <copyright file="DdlValidationSpecBase.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Event;
using Akka.Persistence.Journal;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Streams;
using Akka.Streams.TestKit;
using Akka.TestKit;
using Akka.TestKit.Xunit;
using Akka.TestKit.Xunit.Internals;
using FluentAssertions;
using FluentAssertions.Extensions;
using LinqToDB.Data;
using Xunit;

#nullable enable
namespace Akka.Persistence.Sql.Tests
{
    /// <summary>
    /// Base class for DDL validation tests. Validates that generated DDL files
    /// execute successfully and that the resulting schema works with Akka.Persistence.
    /// </summary>
    public abstract class DdlValidationSpecBase
    {
        protected readonly ITestOutputHelper Output;
        protected readonly ITestContainer Container;
        protected abstract string ProviderName { get; }

        protected DdlValidationSpecBase(ITestOutputHelper output, ITestContainer container)
        {
            Output = output;
            Container = container;
        }

        #region DDL File Helpers

        protected string GetDdlPath(string mapping, string filename)
        {
            var assemblyPath = Path.GetDirectoryName(typeof(DdlValidationSpecBase).Assembly.Location);
            var repoRoot = Path.GetFullPath(Path.Combine(assemblyPath!, "..", "..", "..", "..", ".."));
            var ddlPath = Path.Combine(repoRoot, "docs", "ddl", mapping, ProviderName, filename);

            Output.WriteLine($"Looking for DDL at: {ddlPath}");

            if (!File.Exists(ddlPath))
            {
                throw new FileNotFoundException($"DDL file not found: {ddlPath}");
            }

            return ddlPath;
        }

        protected async Task ExecuteDdlFile(DataConnection connection, string ddlPath)
        {
            var sql = await File.ReadAllTextAsync(ddlPath);
            Output.WriteLine($"Executing DDL from: {Path.GetFileName(ddlPath)}");
            Output.WriteLine($"SQL length: {sql.Length} characters");

            await connection.ExecuteAsync(sql);

            Output.WriteLine($"Successfully executed: {Path.GetFileName(ddlPath)}");
        }

        protected async Task ExecuteAllDdlFiles(DataConnection connection, string mapping)
        {
            await ExecuteDdlFile(connection, GetDdlPath(mapping, "journal.sql"));
            await ExecuteDdlFile(connection, GetDdlPath(mapping, "journal-tags.sql"));
            await ExecuteDdlFile(connection, GetDdlPath(mapping, "snapshot.sql"));
            await ExecuteDdlFile(connection, GetDdlPath(mapping, "metadata.sql"));
        }

        #endregion

        #region Basic DDL Validation Tests

        protected async Task ValidateDdlFiles(string mapping)
        {
            Output.WriteLine($"{ProviderName} container started: {Container.ConnectionString}");
            Output.WriteLine($"Testing {mapping} mapping");

            await Container.InitializeDbAsync();

            await using var connection = new DataConnection(
                Container.ProviderName,
                Container.ConnectionString);

            await ExecuteAllDdlFiles(connection, mapping);

            Output.WriteLine($"All {ProviderName} ({mapping}) DDL files executed successfully");
        }

        protected async Task ValidateDdlIdempotency(string mapping)
        {
            Output.WriteLine($"{ProviderName} container started");
            Output.WriteLine($"Testing {mapping} mapping idempotency");

            await Container.InitializeDbAsync();

            await using var connection = new DataConnection(
                Container.ProviderName,
                Container.ConnectionString);

            var journalPath = GetDdlPath(mapping, "journal.sql");

            await ExecuteDdlFile(connection, journalPath);
            Output.WriteLine("First execution completed");

            await ExecuteDdlFile(connection, journalPath);
            Output.WriteLine("Second execution completed - DDL is idempotent");

            Output.WriteLine($"{ProviderName} ({mapping}) DDL is idempotent");
        }

        #endregion

        #region Integration Test - Full Persistence Validation

        /// <summary>
        /// Integration test that validates the DDL-created schema works correctly
        /// with Akka.Persistence. Tests events, snapshots, recovery, and tag queries.
        /// </summary>
        protected async Task ValidatePersistenceIntegration(string mapping)
        {
            Output.WriteLine($"Starting persistence integration test for {ProviderName} ({mapping})");

            // Step 1: Clean database and execute DDL
            await Container.InitializeDbAsync();

            await using (var connection = new DataConnection(Container.ProviderName, Container.ConnectionString))
            {
                await ExecuteAllDdlFiles(connection, mapping);
            }

            Output.WriteLine("DDL executed successfully, starting persistence tests");

            // Step 2: Create actor system with persistence config (auto-initialize = false)
            var config = CreatePersistenceConfig(mapping);
            var setup = ActorSystemSetup.Create(BootstrapSetup.Create().WithConfig(config));
            using var sys = ActorSystem.Create("DdlValidationTest", setup);

            InitializeLogger(sys);

            var testKit = new Akka.TestKit.Xunit.TestKit(sys);

            try
            {
                // Step 3: Create persistent actor and persist events with tags
                const string persistenceId = "ddl-validation-actor-1";
                var actor1 = sys.ActorOf(Props.Create(() => new TaggedPersistentActor(persistenceId)), "actor-1");

                // Persist events - some with tags, some without
                actor1.Tell(new PersistEvent("plain event 1"));
                await testKit.ExpectMsgAsync<string>(s => s == "ACK", 5.Seconds());

                actor1.Tell(new PersistEvent("a green apple")); // Will be tagged with "green" and "apple"
                await testKit.ExpectMsgAsync<string>(s => s == "ACK", 5.Seconds());

                actor1.Tell(new PersistEvent("a blue banana")); // Will be tagged with "blue" and "banana"
                await testKit.ExpectMsgAsync<string>(s => s == "ACK", 5.Seconds());

                actor1.Tell(new PersistEvent("plain event 2"));
                await testKit.ExpectMsgAsync<string>(s => s == "ACK", 5.Seconds());
                // This triggers a snapshot (every 4 events)
                await testKit.ExpectMsgAsync<string>(s => s == "SNAPSHOT_ACK", 5.Seconds());

                Output.WriteLine("Events persisted, verifying state");

                // Step 4: Verify current state
                var state1 = await actor1.Ask<IReadOnlyList<string>>(new GetState(), 5.Seconds());
                state1.Count.Should().Be(4);
                state1[0].Should().Be("plain event 1");
                state1[1].Should().Be("a green apple");
                state1[2].Should().Be("a blue banana");
                state1[3].Should().Be("plain event 2");

                Output.WriteLine("State verified, killing actor");

                // Step 5: Kill the actor
                await actor1.GracefulStop(5.Seconds());

                Output.WriteLine("Actor killed, recreating to test recovery");

                // Step 6: Recreate actor with same persistence ID - should recover from snapshot
                var actor2 = sys.ActorOf(Props.Create(() => new TaggedPersistentActor(persistenceId)), "actor-2");

                // Step 7: Verify recovery (stashing handles message delivery during recovery)
                var state2 = await actor2.Ask<IReadOnlyList<string>>(new GetState(), 5.Seconds());
                state2.Count.Should().Be(4, "Actor should have recovered all 4 events");
                state2.Should().BeEquivalentTo(state1, "Recovered state should match original");

                Output.WriteLine("Recovery verified successfully");

                // Step 8: Query events by tag
                var readJournal = sys.ReadJournalFor<SqlReadJournal>("akka.persistence.query.journal.sql");

                // Query for "green" tag
                var greenSource = readJournal.CurrentEventsByTag("green", Offset.NoOffset());
                var greenProbe = greenSource.RunWith(testKit.SinkProbe<EventEnvelope>(), sys.Materializer());
                greenProbe.Request(10);
                var greenEvent = await greenProbe.ExpectNextAsync(5.Seconds());
                greenEvent.Event.Should().Be("a green apple");
                await greenProbe.ExpectCompleteAsync();

                Output.WriteLine("Tag query for 'green' successful");

                // Query for "blue" tag
                var blueSource = readJournal.CurrentEventsByTag("blue", Offset.NoOffset());
                var blueProbe = blueSource.RunWith(testKit.SinkProbe<EventEnvelope>(), sys.Materializer());
                blueProbe.Request(10);
                var blueEvent = await blueProbe.ExpectNextAsync(5.Seconds());
                blueEvent.Event.Should().Be("a blue banana");
                await blueProbe.ExpectCompleteAsync();

                Output.WriteLine("Tag query for 'blue' successful");

                // Query all events
                var allSource = readJournal.CurrentEventsByPersistenceId(persistenceId, 0, long.MaxValue);
                var allProbe = allSource.RunWith(testKit.SinkProbe<EventEnvelope>(), sys.Materializer());
                allProbe.Request(10);

                var evt1 = await allProbe.ExpectNextAsync(5.Seconds());
                evt1.Event.Should().Be("plain event 1");
                evt1.SequenceNr.Should().Be(1);

                var evt2 = await allProbe.ExpectNextAsync(5.Seconds());
                evt2.Event.Should().Be("a green apple");
                evt2.SequenceNr.Should().Be(2);

                var evt3 = await allProbe.ExpectNextAsync(5.Seconds());
                evt3.Event.Should().Be("a blue banana");
                evt3.SequenceNr.Should().Be(3);

                var evt4 = await allProbe.ExpectNextAsync(5.Seconds());
                evt4.Event.Should().Be("plain event 2");
                evt4.SequenceNr.Should().Be(4);

                await allProbe.ExpectCompleteAsync();

                Output.WriteLine("All events query successful");

                // Clean up actor2
                await actor2.GracefulStop(5.Seconds());
            }
            finally
            {
                await sys.Terminate();
            }

            Output.WriteLine($"Persistence integration test completed successfully for {ProviderName} ({mapping})");
        }

        #endregion

        #region Configuration

        private Akka.Configuration.Config CreatePersistenceConfig(string mapping)
        {
            var tableMapping = GetTableMapping(mapping);

            return Akka.Configuration.ConfigurationFactory.ParseString(
$@"
akka {{
    loglevel = INFO
    persistence {{
        journal {{
            plugin = ""akka.persistence.journal.sql""
            sql {{
                class = ""Akka.Persistence.Sql.Journal.SqlWriteJournal, Akka.Persistence.Sql""
                connection-string = ""{Container.ConnectionString}""
                provider-name = ""{Container.ProviderName}""
                auto-initialize = false
                table-mapping = ""{tableMapping}""
                tag-write-mode = TagTable
                event-adapters {{
                    ddl-tagger = ""Akka.Persistence.Sql.Tests.DdlTagAdapter, Akka.Persistence.Sql.Tests""
                }}
                event-adapter-bindings {{
                    ""System.String"" = ddl-tagger
                }}
            }}
        }}
        query.journal.sql {{
            class = ""Akka.Persistence.Sql.Query.SqlReadJournalProvider, Akka.Persistence.Sql""
            connection-string = ""{Container.ConnectionString}""
            provider-name = ""{Container.ProviderName}""
            table-mapping = ""{tableMapping}""
            tag-read-mode = TagTable
            refresh-interval = 1s
        }}
        snapshot-store {{
            plugin = ""akka.persistence.snapshot-store.sql""
            sql {{
                class = ""Akka.Persistence.Sql.Snapshot.SqlSnapshotStore, Akka.Persistence.Sql""
                connection-string = ""{Container.ConnectionString}""
                provider-name = ""{Container.ProviderName}""
                auto-initialize = false
                table-mapping = ""{tableMapping}""
            }}
        }}
    }}
}}
akka.test.single-expect-default = 10s
").WithFallback(SqlPersistence.DefaultConfiguration);
        }

        private string GetTableMapping(string mapping)
        {
            // "default" mapping uses "default" table-mapping in HOCON
            // "compat" mapping uses provider-specific legacy table-mapping
            if (mapping == "default")
                return "default";

            return ProviderName switch
            {
                "sqlserver" => "sql-server",
                "postgresql" => "postgresql",
                "mysql" => "mysql",
                "sqlite" => "sqlite",
                _ => throw new ArgumentException($"Unknown provider: {ProviderName}")
            };
        }

        private void InitializeLogger(ActorSystem system)
        {
            var extSystem = (ExtendedActorSystem)system;
            var logger = extSystem.SystemActorOf(Props.Create(() => new TestOutputLogger(Output)), "log-test");
            logger.Ask<LoggerInitialized>(new InitializeLogger(system.EventStream), TimeSpan.FromSeconds(3))
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        #endregion

        #region Messages

        public sealed record PersistEvent(string Data);

        public sealed class GetState
        {
            public static readonly GetState Instance = new();
        }

        #endregion

        #region Persistent Actor

        /// <summary>
        /// Persistent actor for testing DDL-created schema.
        /// </summary>
        private sealed class TaggedPersistentActor : ReceivePersistentActor
        {
            private readonly List<string> _events = new();
            private IActorRef? _sender;

            public TaggedPersistentActor(string persistenceId)
            {
                PersistenceId = persistenceId;

                Recover<SnapshotOffer>(offer =>
                {
                    if (offer.Snapshot is List<string> events)
                    {
                        _events.Clear();
                        _events.AddRange(events);
                    }
                });

                Recover<string>(evt => _events.Add(evt));

                Command<PersistEvent>(cmd =>
                {
                    _sender = Sender;
                    Persist(cmd.Data, evt =>
                    {
                        _events.Add(evt);
                        _sender.Tell("ACK");

                        // Save snapshot every 4 events
                        if (LastSequenceNr % 4 == 0)
                        {
                            SaveSnapshot(new List<string>(_events));
                        }
                    });
                });

                Command<GetState>(_ => Sender.Tell(_events.ToList().AsReadOnly()));

                Command<SaveSnapshotSuccess>(_ => _sender?.Tell("SNAPSHOT_ACK"));

                Command<SaveSnapshotFailure>(failure =>
                {
                    Context.GetLogger().Error(failure.Cause, "Snapshot failed");
                });
            }

            public override string PersistenceId { get; }
        }

        #endregion
    }

    #region Event Adapter

    /// <summary>
    /// Event adapter that adds tags based on keywords in the event data.
    /// </summary>
    public class DdlTagAdapter : IEventAdapter
    {
        private static readonly IImmutableSet<string> Colors = ImmutableHashSet.Create("green", "blue", "red", "black");
        private static readonly IImmutableSet<string> Fruits = ImmutableHashSet.Create("apple", "banana", "orange");

        public string Manifest(object evt) => string.Empty;

        public object ToJournal(object evt)
        {
            if (evt is not string s)
                return evt;

            var colorTags = Colors.Where(c => s.Contains(c)).ToImmutableHashSet();
            var fruitTags = Fruits.Where(f => s.Contains(f)).ToImmutableHashSet();
            var tags = colorTags.Union(fruitTags);

            return tags.IsEmpty ? evt : new Tagged(evt, tags);
        }

        public IEventSequence FromJournal(object evt, string manifest)
            => EventSequence.Single(evt);
    }

    #endregion
}
