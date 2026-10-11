using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Journal.Dao;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Snapshot;
using Akka.Persistence.Sql.Utility;
using Akka.Streams.Dsl;
using Akka.Util;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Sqlite;

public class SqliteMaterializerLifetimeSpec : Akka.TestKit.Xunit.TestKit
{
    private static readonly Configuration.Config TestConfig = ConfigurationFactory.ParseString($$"""
        akka.persistence.journal {
            plugin = akka.persistence.journal.sql
            sql {
                class = "{{typeof(RestartableSqlWriteJournal).AssemblyQualifiedName}}"
                auto-initialize = false
                provider-name = SQLite.MS
                connection-string = "Data Source=:memory:"
            }
        }
        akka.persistence.snapshot-store {
            plugin = akka.persistence.snapshot-store.sql
            sql {
                class = "Akka.Persistence.Sql.Snapshot.SqlSnapshotStore, Akka.Persistence.Sql"
                auto-initialize = false
                provider-name = SQLite.MS
                connection-string = "Data Source=:memory:"
            }
        }
        """).WithFallback(SqlPersistence.DefaultConfiguration);

    public SqliteMaterializerLifetimeSpec(ITestOutputHelper output) : base(TestConfig.ToString(), output) { }

    [Fact]
    public async Task Journal_supervisor_is_a_child_and_terminates_on_restart_and_stop()
    {
        var journal = Persistence.Instance.Apply(Sys).JournalFor(null);
        await journal.Ask<Initialized>(IsInitialized.Instance, TimeSpan.FromSeconds(5));

        var originalSupervisor = await SupervisorOf(journal);
        var probe = CreateTestProbe();
        probe.Watch(originalSupervisor);

        journal.Tell(RestartJournal.Instance);
        probe.ExpectTerminated(originalSupervisor, TimeSpan.FromSeconds(5));

        await journal.Ask<Initialized>(IsInitialized.Instance, TimeSpan.FromSeconds(5));
        var replacementSupervisor = await SupervisorOf(journal);
        Assert.NotEqual(originalSupervisor, replacementSupervisor);

        probe.Watch(replacementSupervisor);
        Sys.Stop(journal);
        probe.ExpectTerminated(replacementSupervisor, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Snapshot_supervisor_is_a_child_and_terminates_with_store()
    {
        var store = Persistence.Instance.Apply(Sys).SnapshotStoreFor(null);
        var supervisor = await SupervisorOf(store);

        var probe = CreateTestProbe();
        probe.Watch(supervisor);
        Sys.Stop(store);
        probe.ExpectTerminated(supervisor, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Sequence_actor_supervisor_is_a_child_and_terminates_with_actor()
    {
        var config = new JournalSequenceRetrievalConfig(ConfigurationFactory.ParseString("{}"));
        var actor = Sys.ActorOf(Props.Create(() => new JournalSequenceActor(new EmptyReadJournalDao(), config)));
        var supervisor = await SupervisorOf(actor);

        var probe = CreateTestProbe();
        probe.Watch(supervisor);
        Sys.Stop(actor);
        probe.ExpectTerminated(supervisor, TimeSpan.FromSeconds(5));
    }

    private async Task<IActorRef> SupervisorOf(IActorRef owner)
    {
        await owner.Ask<ActorIdentity>(new Identify(null), TimeSpan.FromSeconds(5));
        var identity = await Sys.ActorSelection($"{owner.Path}/StreamSupervisor-*")
            .Ask<ActorIdentity>(new Identify(null), TimeSpan.FromSeconds(5));
        Assert.NotNull(identity.Subject);
        Assert.Equal(owner.Path, identity.Subject.Path.Parent);
        return identity.Subject;
    }

    private sealed class EmptyReadJournalDao : IReadJournalDao
    {
        public Source<string, NotUsed> AllPersistenceIdsSource(long max) => Source.Empty<string>();

        public Source<Try<(IPersistentRepresentation, string[], long)>, NotUsed> EventsByTag(
            string tag, long offset, long maxOffset, long max) => throw new NotSupportedException();

        public Source<long, NotUsed> JournalSequence(long offset, long limit) => Source.Empty<long>();

        public Task<long> MaxJournalSequenceAsync() => Task.FromResult(0L);

        public Task<long> FromEndStartOffsetAsync(string tag, int count) => throw new NotSupportedException();

        public Task<Source<Try<ReplayCompletion>, NotUsed>> Messages(
            string persistenceId, long fromSequenceNr, long toSequenceNr, long max) => throw new NotSupportedException();

        public Source<Try<ReplayCompletion>, NotUsed> MessagesWithBatch(
            string persistenceId, long fromSequenceNr, long toSequenceNr, int batchSize,
            Option<(TimeSpan duration, IScheduler scheduler)> refreshInterval) => throw new NotSupportedException();
    }
}

internal sealed class RestartJournal
{
    public static readonly RestartJournal Instance = new();
}

public sealed class RestartableSqlWriteJournal(Configuration.Config config) : SqlWriteJournal(config)
{
    protected override bool ReceivePluginInternal(object message)
    {
        if (message is RestartJournal)
            throw new ApplicationException("Test-controlled journal restart");

        return base.ReceivePluginInternal(message);
    }
}
