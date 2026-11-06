// -----------------------------------------------------------------------
//  <copyright file="MsSqliteReplayFilterWriterUuidSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Tests.Common.Containers;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Sqlite
{
    /// <summary>
    ///     The journal writes one writer UUID per journal actor (so per node) on every row,
    ///     while Akka's ReplayFilter treats the writer UUID as one persistent actor incarnation.
    ///     When a persistence id moves node A -> node B -> back to node A (A still running),
    ///     A's later events look like an "old writer" and are discarded on every replay
    ///     under the default replay-filter mode (repair-by-discard-old).
    ///     Two journal plugin ids on one database stand in for two nodes.
    /// </summary>
    [Collection(nameof(MsSqlitePersistenceSpec))]
    public class MsSqliteReplayFilterWriterUuidSpec : Akka.TestKit.Xunit.TestKit
    {
        private const string NodeA = "akka.persistence.journal.sql";
        private const string NodeB = "akka.persistence.journal.sql-b";

        public MsSqliteReplayFilterWriterUuidSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(CreateConfig(fixture), nameof(MsSqliteReplayFilterWriterUuidSpec), output) { }

        private static Configuration.Config CreateConfig(MsSqliteContainer fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            return ConfigurationFactory.ParseString($$"""
                akka.persistence.journal {
                    plugin = "{{NodeA}}"
                    sql {
                        connection-string = "{{fixture.ConnectionString}}"
                        provider-name = "{{fixture.ProviderName}}"
                    }
                    sql-b {
                        class = "{{typeof(SqlWriteJournal).AssemblyQualifiedName}}"
                        connection-string = "{{fixture.ConnectionString}}"
                        provider-name = "{{fixture.ProviderName}}"
                    }
                }
                """)
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }

        [Fact]
        public async Task Events_persisted_again_through_an_earlier_journal_are_replayed()
        {
            const string persistenceId = "entity-1";

            await PersistThenStop(persistenceId, NodeA, "a-1", "a-2");
            await PersistThenStop(persistenceId, NodeB, "b-3");
            await PersistThenStop(persistenceId, NodeA, "a-4");

            var reader = Sys.ActorOf(Props.Create(() => new EventsActor(persistenceId, NodeB)));
            var replayed = await reader.Ask<string[]>(EventsActor.GetEvents, 5.Seconds());

            replayed.Should().Equal("a-1", "a-2", "b-3", "a-4");
        }

        private async Task PersistThenStop(string persistenceId, string journalPluginId, params string[] events)
        {
            var actor = Sys.ActorOf(Props.Create(() => new EventsActor(persistenceId, journalPluginId)));
            foreach (var e in events)
                (await actor.Ask<string>(e, 5.Seconds())).Should().Be(e);

            var probe = CreateTestProbe();
            probe.Watch(actor);
            Sys.Stop(actor);
            await probe.ExpectTerminatedAsync(actor, 5.Seconds());
        }

        private sealed class EventsActor : ReceivePersistentActor
        {
            public const string GetEvents = "get-events";
            private readonly List<string> _events = new();

            public EventsActor(string persistenceId, string journalPluginId)
            {
                PersistenceId = persistenceId;
                JournalPluginId = journalPluginId;

                Recover<string>(e => _events.Add(e));
                Command<string>(cmd => cmd == GetEvents, _ => Sender.Tell(_events.ToArray()));
                Command<string>(cmd => Persist(cmd, e =>
                {
                    _events.Add(e);
                    Sender.Tell(e);
                }));
            }

            public override string PersistenceId { get; }
        }
    }
}
