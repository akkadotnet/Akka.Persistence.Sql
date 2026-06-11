// -----------------------------------------------------------------------
//  <copyright file="MsSqliteFromEndSpec.cs" company="Akka.NET Project">
//      SPIKE ONLY: validates the FromEnd ("last N events") query offset against a real SQL backend.
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Persistence;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Query;
using Akka.Persistence.Sql.Tests.Sqlite;
using Akka.Streams.Dsl;
using Akka.Streams.TestKit;
using FluentAssertions;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Query.MsSqlite
{
    // minimal persistent actor: persists string commands (tagged "green"/"apple" by the configured ColorFruitTagger)
    internal sealed class FromEndTestActor : UntypedPersistentActor
    {
        public static Props Props(string persistenceId) => Actor.Props.Create(() => new FromEndTestActor(persistenceId));

        public FromEndTestActor(string persistenceId) => PersistenceId = persistenceId;

        public override string PersistenceId { get; }

        protected override void OnRecover(object message) { }

        protected override void OnCommand(object message)
        {
            if (message is string s)
            {
                var sender = Sender;
                Persist(s, e => sender.Tell($"{e}-done"));
            }
        }
    }

    [Collection(nameof(MsSqlitePersistenceSpec))]
    public class MsSqliteFromEndSpec : BaseCurrentEventsByTagSpec<MsSqliteContainer>
    {
        public MsSqliteFromEndSpec(ITestOutputHelper output, MsSqliteContainer fixture)
            : base(TagMode.TagTable, output, nameof(MsSqliteFromEndSpec), fixture)
        {
        }

        [Fact]
        public void CurrentEventsByTag_with_FromEnd_should_return_only_the_last_N_events()
        {
            var queries = (ICurrentEventsByTagQuery)ReadJournal;

            var a = Sys.ActorOf(FromEndTestActor.Props("from-end-a"));
            for (var i = 1; i <= 10; i++)
            {
                a.Tell($"a green apple {i}");
                ExpectMsg($"a green apple {i}-done");
            }

            WaitForGreen(queries, 10);

            var probe = queries
                .CurrentEventsByTag("green", Offset.FromEnd(3))
                .RunWith(this.SinkProbe<EventEnvelope>(), Materializer);

            probe.Request(10);
            probe.ExpectNext<EventEnvelope>(e => e.SequenceNr == 8L);
            probe.ExpectNext<EventEnvelope>(e => e.SequenceNr == 9L);
            probe.ExpectNext<EventEnvelope>(e => e.SequenceNr == 10L);
            probe.ExpectComplete();
        }

        [Fact]
        public void CurrentEventsByTag_with_FromEnd_larger_than_total_should_return_all_events()
        {
            var queries = (ICurrentEventsByTagQuery)ReadJournal;

            var a = Sys.ActorOf(FromEndTestActor.Props("from-end-b"));
            for (var i = 1; i <= 5; i++)
            {
                a.Tell($"a green apple {i}");
                ExpectMsg($"a green apple {i}-done");
            }

            WaitForGreen(queries, 5);

            var probe = queries
                .CurrentEventsByTag("green", Offset.FromEnd(100))
                .RunWith(this.SinkProbe<EventEnvelope>(), Materializer);

            probe.Request(10);
            for (var i = 1L; i <= 5L; i++)
                probe.ExpectNext<EventEnvelope>(e => e.SequenceNr == i);
            probe.ExpectComplete();
        }

        private void WaitForGreen(ICurrentEventsByTagQuery queries, int expected)
            => AwaitConditionAsync(async () =>
            {
                var all = await queries
                    .CurrentEventsByTag("green", Offset.NoOffset())
                    .RunWith(Sink.Seq<EventEnvelope>(), Materializer);
                return all.Count >= expected;
            }, TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
    }
}
