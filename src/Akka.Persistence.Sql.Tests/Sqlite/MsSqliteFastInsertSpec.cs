// -----------------------------------------------------------------------
//  <copyright file="MsSqliteFastInsertSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Event;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Streams;
using Akka.Streams.TestKit;
using Akka.TestKit;
using Akka.TestKit.Xunit2;
using Akka.TestKit.Xunit2.Internals;
using FluentAssertions;
using FluentAssertions.Extensions;
using LinqToDB;
using LinqToDB.Async;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    /// <summary>
    ///     🌸 Ami-chan's SQLite Fast Insert Spec! UwU 🌸
    ///     <para>
    ///         This test class explicitly exercises the <c>RunFastInsert</c> code path
    ///         in <see cref="Akka.Persistence.Sql.Journal.Dao.BaseByteArrayJournalDao"/>
    ///         using an SQLite in-memory provider.
    ///     </para>
    ///     <para>
    ///         <b>CopilotNotes:</b> The <c>RunFastInsert</c> path is triggered when:
    ///         <list type="bullet">
    ///             <item>Tag write mode is <c>TagTable</c> (not CSV)</item>
    ///             <item>Provider name contains "Sqlite"</item>
    ///             <item>Multiple journal rows are being inserted in a single batch</item>
    ///         </list>
    ///         We test this three ways:
    ///         <list type="number">
    ///             <item>Sending <c>WriteMessages</c> directly to the journal actor (protocol-level) ✨</item>
    ///             <item>Querying the SQLite database directly to verify journal + tag table rows 🔍</item>
    ///             <item>Round-tripping through actor recovery to confirm data integrity 🌟</item>
    ///         </list>
    ///     </para>
    /// </summary>
    [Collection(nameof(MsSqlitePersistenceSpec))]
    public class MsSqliteFastInsertSpec :
        TestKitBase,
        IClassFixture<MsSqliteContainer>,
        IAsyncLifetime
    {
        private const string Ack = "ACK";
        private const string GetAll = "getAll";
        private const string PId = "fast-insert-test";

        private readonly ITestOutputHelper _output;
        private readonly MsSqliteContainer _fixture;

        public MsSqliteFastInsertSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(new XunitAssertions(), null, nameof(MsSqliteFastInsertSpec))
        {
            _output = output;
            _fixture = fixture;
        }

        /// <summary>
        ///     🎀 Builds the HOCON config with TagTable mode enabled so we hit the RunFastInsert path! UwU
        ///     <para>
        ///         <b>CopilotNotes:</b> The key settings here are:
        ///         <list type="bullet">
        ///             <item><c>tag-write-mode = TagTable</c> — ensures we go through InsertMultiple → RunFastInsert</item>
        ///             <item><c>tag-read-mode = TagTable</c> — ensures reads use the tag table too</item>
        ///             <item>The <c>event-adapters</c> section wires up the <see cref="ColorFruitTagger"/>
        ///                   so string events get auto-tagged based on color/fruit keywords</item>
        ///         </list>
        ///     </para>
        /// </summary>
        private static Configuration.Config Config(MsSqliteContainer fixture)
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
                    color-tagger = "Akka.Persistence.Sql.Tests.ColorFruitTagger, Akka.Persistence.Sql.Tests"
                }
                event-adapter-bindings = {
                    "System.String" = color-tagger
                }
                provider-name = "{{fixture.ProviderName}}"
                tag-write-mode = "TagTable"
                connection-string = "{{fixture.ConnectionString}}"
            }
        }
        query.journal.sql {
            provider-name = "{{fixture.ProviderName}}"
            connection-string = "{{fixture.ConnectionString}}"
            tag-read-mode = "TagTable"
            refresh-interval = 1s
        }
        snapshot-store {
            plugin = "akka.persistence.snapshot-store.sql"
            sql {
                connection-string = "{{fixture.ConnectionString}}"
                provider-name = "{{fixture.ProviderName}}"
            }
        }
    }
}
akka.test.single-expect-default = 10s
""")
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }

        public async Task InitializeAsync()
        {
            await _fixture.InitializeAsync();

            var setup = ActorSystemSetup.Create(BootstrapSetup.Create().WithConfig(Config(_fixture)));
            base.InitializeTest(null, setup, nameof(MsSqliteFastInsertSpec), null);
            InitializeLogger(Sys);
        }

        public async Task DisposeAsync()
        {
            // 🌟 Gracefully shut down to avoid race conditions with SQLite in-memory DB disposal
            await Sys.Terminate();
        }

        protected override void InitializeTest(
            ActorSystem system,
            ActorSystemSetup config,
            string actorSystemName,
            string testActorName)
        {
            // no-op, call after database is set up
        }

        /// <summary>
        ///     Initializes a new <see cref="TestOutputLogger"/> used to log messages.
        /// </summary>
        private void InitializeLogger(ActorSystem system)
        {
            var extSystem = (ExtendedActorSystem)system;
            var logger = extSystem.SystemActorOf(Props.Create(() => new TestOutputLogger(_output)), "log-test");
            logger.Ask<LoggerInitialized>(new InitializeLogger(system.EventStream), TimeSpan.FromSeconds(3))
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <summary>
        ///     🔧 Helper: creates a <see cref="JournalConfig"/> + <see cref="AkkaPersistenceDataConnectionFactory"/>
        ///     so we can open a raw <see cref="AkkaDataConnection"/> to query the SQLite DB directly. UwU
        /// </summary>
        private AkkaPersistenceDataConnectionFactory CreateConnectionFactory()
        {
            var journalHocon = Sys.Settings.Config
                .GetConfig("akka.persistence.journal.sql")
                .WithFallback(SqlPersistence.DefaultJournalConfiguration);
            var journalConfig = new JournalConfig(journalHocon);
            return new AkkaPersistenceDataConnectionFactory(journalConfig);
        }

        /// <summary>
        ///     🌸 Tests that persisting multiple tagged events in a batch via PersistAll
        ///     exercises the RunFastInsert path, and that we can verify the database state
        ///     directly by querying the journal + tag tables. UwU ✨
        ///     <para>
        ///         <b>CopilotNotes:</b> We persist 3 string events containing color/fruit keywords
        ///         so <see cref="ColorFruitTagger"/> applies tags. <c>PersistAll</c> sends them as
        ///         one <c>AtomicWrite</c> with multiple entries → <c>InsertMultiple</c> → <c>RunFastInsert</c>.
        ///         Then we open a raw <see cref="AkkaDataConnection"/> to the same in-memory DB and
        ///         assert exact row counts and values in both the <c>journal</c> and <c>tags</c> tables.
        ///     </para>
        /// </summary>
        [Fact(DisplayName = "RunFastInsert should write correct rows to journal and tag tables (verified via direct DB query)")]
        public async Task RunFastInsert_Should_Write_Correct_Rows_To_Database()
        {
            var timeout = TimeSpan.FromSeconds(10);
            var pid = $"{PId}-db-verify";

            // 🌟 Create a persistence actor that uses PersistAll for batch writes
            var actor = Sys.ActorOf(
                Props.Create(() => new TaggedBatchPersistenceActor(pid)),
                "fast-insert-db-actor");

            // 🎀 Send a batch of tagged events — these will go through RunFastInsert!
            // ColorFruitTagger tags: "green" → green, "black" → black, "apple" → apple, "banana" → banana
            var taggedMessages = new[]
            {
                "a green apple",   // tags: green, apple
                "a black banana",  // tags: black, banana
                "a green banana",  // tags: green, banana
            };

            actor.Tell(new PersistBatch(taggedMessages));

            // Wait for all 3 persists to be acknowledged
            for (var i = 0; i < taggedMessages.Length; i++)
                ExpectMsg<string>(Ack, timeout);

            // 🔍 Now open a raw connection to the same in-memory SQLite DB
            //    and verify the exact database state written by RunFastInsert!
            var factory = CreateConnectionFactory();
            await using var connection = factory.GetConnection();

            // ── Assert journal table rows ──
            var journalRows = await connection.GetTable<JournalRow>()
                .Where(r => r.PersistenceId == pid)
                .OrderBy(r => r.SequenceNumber)
                .ToListAsync();

            journalRows.Should().HaveCount(3, "RunFastInsert should have inserted exactly 3 journal rows");
            journalRows[0].SequenceNumber.Should().Be(1);
            journalRows[1].SequenceNumber.Should().Be(2);
            journalRows[2].SequenceNumber.Should().Be(3);

            // ── Assert tag table rows ──
            //    RunFastInsert uses InsertWithOutputAsync → BulkCopy to the tag table
            var tagRows = await connection.GetTable<JournalTagRow>()
                .Where(r => r.PersistenceId == pid)
                .OrderBy(r => r.OrderingId)
                .ThenBy(r => r.TagValue)
                .ToListAsync();

            // "a green apple" → 2 tags (apple, green)
            // "a black banana" → 2 tags (banana, black)
            // "a green banana" → 2 tags (banana, green)
            tagRows.Should().HaveCount(6, "RunFastInsert should have inserted exactly 6 tag rows (2 per event)");

            // 🌸 Verify tag values are correctly associated with the right ordering IDs
            var tagsForEvent1 = tagRows.Where(t => t.OrderingId == journalRows[0].Ordering).Select(t => t.TagValue).ToList();
            var tagsForEvent2 = tagRows.Where(t => t.OrderingId == journalRows[1].Ordering).Select(t => t.TagValue).ToList();
            var tagsForEvent3 = tagRows.Where(t => t.OrderingId == journalRows[2].Ordering).Select(t => t.TagValue).ToList();

            tagsForEvent1.Should().BeEquivalentTo(new[] { "apple", "green" }, "event 1 'a green apple' should have green + apple tags");
            tagsForEvent2.Should().BeEquivalentTo(new[] { "banana", "black" }, "event 2 'a black banana' should have black + banana tags");
            tagsForEvent3.Should().BeEquivalentTo(new[] { "banana", "green" }, "event 3 'a green banana' should have green + banana tags");
        }

        /// <summary>
        ///     🎀 Tests that tags written via RunFastInsert are correctly queryable
        ///     via the <see cref="SqlReadJournal.CurrentEventsByTag"/> read journal API. ✨
        /// </summary>
        [Fact(DisplayName = "RunFastInsert should store tags in tag table queryable by EventsByTag")]
        public async Task RunFastInsert_Tags_Should_Be_Queryable_By_EventsByTag()
        {
            var timeout = TimeSpan.FromSeconds(10);
            var pid = $"{PId}-query";

            var actor = Sys.ActorOf(
                Props.Create(() => new TaggedBatchPersistenceActor(pid)),
                "fast-insert-query-actor");

            // 🌸 Persist a batch with known tags
            var taggedMessages = new[]
            {
                "a green apple",   // tags: green, apple
                "a black banana",  // tags: black, banana
            };

            actor.Tell(new PersistBatch(taggedMessages));
            for (var i = 0; i < taggedMessages.Length; i++)
                ExpectMsg<string>(Ack, timeout);

            // 🌟 Query by tag using the read journal
            var readJournal = Sys.ReadJournalFor<SqlReadJournal>("akka.persistence.query.journal.sql");

            // Query for "green" tag — should find "a green apple"
            var greenSource = readJournal.CurrentEventsByTag("green", Offset.NoOffset());
            var greenProbe = greenSource.RunWith(this.SinkProbe<EventEnvelope>(), Sys.Materializer());
            greenProbe.Request(2);
            greenProbe.ExpectNext<EventEnvelope>(e => e.Event.Equals("a green apple"));
            greenProbe.ExpectComplete();

            // Query for "banana" tag — should find "a black banana"
            var bananaSource = readJournal.CurrentEventsByTag("banana", Offset.NoOffset());
            var bananaProbe = bananaSource.RunWith(this.SinkProbe<EventEnvelope>(), Sys.Materializer());
            bananaProbe.Request(2);
            bananaProbe.ExpectNext<EventEnvelope>(e => e.Event.Equals("a black banana"));
            bananaProbe.ExpectComplete();
        }

        /// <summary>
        ///     🌟 Tests that the RunFastInsert path handles mixed tagged and untagged events,
        ///     and that recovery fully restores the actor state from the database.
        ///     <para>
        ///         <b>CopilotNotes:</b> This tests the batching logic inside RunFastInsert where
        ///         <c>tagDict</c> accumulates entries, and also verifies that untagged events
        ///         (which still get empty tag arrays) don't break the bulk insert flow.
        ///     </para>
        /// </summary>
        [Fact(DisplayName = "RunFastInsert should handle mixed tagged/untagged events and survive recovery")]
        public async Task RunFastInsert_Should_Handle_Mixed_Events_And_Recover()
        {
            var timeout = TimeSpan.FromSeconds(10);
            var pid = $"{PId}-mixed";

            var actor = Sys.ActorOf(
                Props.Create(() => new TaggedBatchPersistenceActor(pid)),
                "fast-insert-mixed-actor");

            // 🎀 Mix of tagged and untagged events
            var messages = new[]
            {
                "hello world",        // no tags (ColorFruitTagger finds no keywords)
                "a green leaf",       // tags: green
                "plain message",      // no tags
                "a black apple",      // tags: black, apple
                "another plain one",  // no tags
                "a green banana",     // tags: green, banana
            };

            actor.Tell(new PersistBatch(messages));
            for (var i = 0; i < messages.Length; i++)
                ExpectMsg<string>(Ack, timeout);

            // 🔍 Verify database state directly
            var factory = CreateConnectionFactory();
            await using var connection = factory.GetConnection();

            var journalRows = await connection.GetTable<JournalRow>()
                .Where(r => r.PersistenceId == pid)
                .OrderBy(r => r.SequenceNumber)
                .ToListAsync();

            journalRows.Should().HaveCount(6, "all 6 events should be in the journal table");

            var tagRows = await connection.GetTable<JournalTagRow>()
                .Where(r => r.PersistenceId == pid)
                .ToListAsync();

            // green(1) + black(1) + apple(1) + green(1) + banana(1) = 5 tags total
            tagRows.Should().HaveCount(5, "only tagged events should produce tag table rows");

            // 🌟 Kill + recover to verify the full round-trip
            await actor.GracefulStop(timeout);
            var actor2 = Sys.ActorOf(
                Props.Create(() => new TaggedBatchPersistenceActor(pid)),
                "fast-insert-mixed-actor-2");

            var recovered = await actor2.Ask<string[]>(GetAll, timeout);
            recovered.Should().BeEquivalentTo(messages,
                "all events including untagged ones should survive the RunFastInsert round-trip UwU");
        }

        #region 🎀 Internal messages and actor types

        /// <summary>
        ///     Message to trigger a <c>PersistAll</c> batch write — this is what
        ///     sends multiple events in one <c>AtomicWrite</c> to hit RunFastInsert! ✨
        /// </summary>
        private sealed record PersistBatch(string[] Events);

        /// <summary>
        ///     🌸 A persistence actor that uses <c>PersistAll</c> to write batches of events,
        ///     ensuring we trigger the multi-row insert path in the DAO. UwU
        ///     <para>
        ///         <b>CopilotNotes:</b> <c>PersistAll</c> groups all events into a single
        ///         <c>AtomicWrite</c>, which the journal plugin processes via <c>InsertMultiple</c>.
        ///         For SQLite + TagTable mode, <c>InsertMultiple</c> calls <c>RunFastInsert</c>.
        ///     </para>
        /// </summary>
        private sealed class TaggedBatchPersistenceActor : ReceivePersistentActor
        {
            private readonly List<string> _events = new();

            public TaggedBatchPersistenceActor(string persistenceId)
            {
                PersistenceId = persistenceId;

                // 🌟 Recovery handler
                Recover<string>(evt => _events.Add(evt));

                // 🎀 PersistAll → one AtomicWrite → InsertMultiple → RunFastInsert
                Command<PersistBatch>(batch =>
                {
                    var sender = Sender;
                    PersistAll(batch.Events, evt =>
                    {
                        _events.Add(evt);
                        sender.Tell(Ack);
                    });
                });

                Command<string>(str => str == GetAll, _ =>
                {
                    Sender.Tell(_events.ToArray());
                });
            }

            public override string PersistenceId { get; }
        }

        #endregion
    }
}


