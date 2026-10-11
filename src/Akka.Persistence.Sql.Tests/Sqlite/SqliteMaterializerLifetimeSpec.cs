using System;
using System.IO;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
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
    private static readonly string MissingDatabase = $"missing-journal-{Guid.NewGuid():N}.db";
    private static readonly string RecoveryDirectory = Path.Combine(Path.GetTempPath(), $"sql-retry-{Guid.NewGuid():N}");
    private static readonly string SnapshotRecoveryDirectory = Path.Combine(Path.GetTempPath(), $"sql-snapshot-retry-{Guid.NewGuid():N}");
    // Forward slashes keep absolute Windows paths valid inside quoted HOCON strings.
    private static readonly string RecoveryDatabasePath = Path.Combine(RecoveryDirectory, "recovery.db").Replace('\\', '/');
    private static readonly string SnapshotRecoveryDatabasePath = Path.Combine(SnapshotRecoveryDirectory, "recovery.db").Replace('\\', '/');

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
        akka.persistence.journal.sql-failing {
            class = "{{typeof(FailingSqlWriteJournal).AssemblyQualifiedName}}"
            auto-initialize = true
            provider-name = SQLite.MS
            connection-string = "Data Source={{MissingDatabase}};Mode=ReadOnly"
        }
        akka.persistence.journal.sql-recovery {
            class = "{{typeof(FailingSqlWriteJournal).AssemblyQualifiedName}}"
            auto-initialize = true
            provider-name = SQLite.MS
            connection-string = "Data Source={{RecoveryDatabasePath}};Mode=ReadWriteCreate;Pooling=False"
        }
        akka.persistence.journal.sql-stop {
            class = "{{typeof(FailingSqlWriteJournal).AssemblyQualifiedName}}"
            auto-initialize = true
            initialization-retry-delay = 2s
            provider-name = SQLite.MS
            connection-string = "Data Source={{MissingDatabase}};Mode=ReadOnly"
        }
        akka.persistence.snapshot-store.sql-recovery {
            class = "{{typeof(FailingSqlSnapshotStore).AssemblyQualifiedName}}"
            auto-initialize = true
            provider-name = SQLite.MS
            connection-string = "Data Source={{SnapshotRecoveryDatabasePath}};Mode=ReadWriteCreate;Pooling=False"
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
    public void Failed_journal_initialization_terminates_each_restarted_supervisor()
    {
        Sys.ActorOf(Props.Create(() => new SupervisorForwarder(TestActor)), "lifetime-observer");
        var journal = Persistence.Instance.Apply(Sys).JournalFor("akka.persistence.journal.sql-failing");
        var probe = CreateTestProbe();

        try
        {
            var firstReport = ExpectMsg<FailedInitSupervisor>(TimeSpan.FromSeconds(5));
            Assert.True(firstReport.IsInitializationFailure);
            var first = firstReport.Supervisor;
            Assert.Equal(journal.Path, first.Path.Parent);
            Assert.StartsWith("StreamSupervisor-", first.Path.Name);
            probe.Watch(first);
            probe.ExpectTerminated(first, TimeSpan.FromSeconds(5));

            var secondReport = ExpectMsg<FailedInitSupervisor>(TimeSpan.FromSeconds(5));
            Assert.True(secondReport.IsInitializationFailure);
            var second = secondReport.Supervisor;
            Assert.Equal(journal.Path, second.Path.Parent);
            Assert.StartsWith("StreamSupervisor-", second.Path.Name);
            Assert.NotEqual(first, second);
            probe.Watch(second);
            probe.ExpectTerminated(second, TimeSpan.FromSeconds(5));
        }
        finally
        {
            Sys.Stop(journal);
        }
    }

    [Fact]
    public async Task Failed_initialization_is_rate_limited_and_recovers_when_SQLite_becomes_available()
    {
        Sys.ActorOf(Props.Create(() => new SupervisorForwarder(TestActor)), "lifetime-observer");
        var journal = Persistence.Instance.Apply(Sys).JournalFor("akka.persistence.journal.sql-recovery");

        try
        {
            var first = ExpectMsg<FailedInitSupervisor>(TimeSpan.FromSeconds(5));
            var second = ExpectMsg<FailedInitSupervisor>(TimeSpan.FromSeconds(5));
            Assert.True(first.IsInitializationFailure);
            Assert.True(second.IsInitializationFailure);
            Assert.True(second.ObservedAt - first.ObservedAt >= TimeSpan.FromMilliseconds(800));

            await Assert.ThrowsAsync<AskTimeoutException>(() =>
                journal.Ask<Initialized>(IsInitialized.Instance, TimeSpan.FromMilliseconds(200)));

            Directory.CreateDirectory(RecoveryDirectory);
            await journal.Ask<Initialized>(IsInitialized.Instance, TimeSpan.FromSeconds(10));
        }
        finally
        {
            var probe = CreateTestProbe();
            probe.Watch(journal);
            Sys.Stop(journal);
            probe.ExpectTerminated(journal, TimeSpan.FromSeconds(5));
            if (Directory.Exists(RecoveryDirectory))
                Directory.Delete(RecoveryDirectory, recursive: true);
        }
    }

    [Fact]
    public void Stopping_failed_journal_cancels_its_pending_retry()
    {
        Sys.EventStream.Subscribe(TestActor, typeof(Error));
        var journal = Persistence.Instance.Apply(Sys).JournalFor("akka.persistence.journal.sql-stop");
        var probe = CreateTestProbe();
        probe.Watch(journal);

        var initializationError = ExpectMsg<Error>(TimeSpan.FromSeconds(5));
        Assert.Contains(journal.Path.ToString(), initializationError.LogSource);
        Assert.Contains("Failure during SQL Journal initialization.", initializationError.Message.ToString());
        Assert.NotNull(initializationError.Cause);
        Sys.Stop(journal);
        probe.ExpectTerminated(journal, TimeSpan.FromSeconds(5));
        ExpectNoMsg(TimeSpan.FromMilliseconds(2200));
    }

    [Fact]
    public async Task Snapshot_initialization_is_rate_limited_and_recovers_when_SQLite_becomes_available()
    {
        Sys.ActorOf(Props.Create(() => new SupervisorForwarder(TestActor)), "lifetime-observer");
        var store = Persistence.Instance.Apply(Sys).SnapshotStoreFor("akka.persistence.snapshot-store.sql-recovery");

        try
        {
            var first = ExpectMsg<FailedInitSupervisor>(TimeSpan.FromSeconds(5));
            var second = ExpectMsg<FailedInitSupervisor>(TimeSpan.FromSeconds(5));
            Assert.True(first.IsInitializationFailure);
            Assert.True(second.IsInitializationFailure);
            Assert.True(second.ObservedAt - first.ObservedAt >= TimeSpan.FromMilliseconds(800));

            var metadata = new SnapshotMetadata("snapshot-recovery", 1, DateTime.UtcNow);
            await Assert.ThrowsAsync<AskTimeoutException>(() =>
                store.Ask<SaveSnapshotSuccess>(new SaveSnapshot(metadata, "not yet"), TimeSpan.FromMilliseconds(200)));

            Directory.CreateDirectory(SnapshotRecoveryDirectory);
            var saved = await store.Ask<SaveSnapshotSuccess>(
                new SaveSnapshot(new SnapshotMetadata("snapshot-recovery", 2, DateTime.UtcNow), "saved"),
                TimeSpan.FromSeconds(10));
            Assert.Equal(2, saved.Metadata.SequenceNr);
        }
        finally
        {
            var probe = CreateTestProbe();
            probe.Watch(store);
            Sys.Stop(store);
            probe.ExpectTerminated(store, TimeSpan.FromSeconds(5));
            if (Directory.Exists(SnapshotRecoveryDirectory))
                Directory.Delete(SnapshotRecoveryDirectory, recursive: true);
        }
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

internal sealed record FailedInitSupervisor(IActorRef Supervisor, bool IsInitializationFailure, DateTimeOffset ObservedAt);

public sealed class SupervisorForwarder : ReceiveActor
{
    public SupervisorForwarder(IActorRef target)
    {
        Receive<FailedInitSupervisor>(message => target.Tell(message));
    }
}

public sealed class FailingSqlWriteJournal(Configuration.Config config) : SqlWriteJournal(config)
{
    public override void AroundPreRestart(Exception cause, object? message)
    {
        var isInitializationFailure = cause is ApplicationException { InnerException: not null }
                                      && cause.Message == "Failed to initialize SQL Journal.";
        foreach (var child in Context.GetChildren())
        {
            if (child.Path.Name.StartsWith("StreamSupervisor-", StringComparison.Ordinal))
                Context.ActorSelection("/user/lifetime-observer")
                    .Tell(new FailedInitSupervisor(child, isInitializationFailure, DateTimeOffset.UtcNow));
        }

        base.AroundPreRestart(cause, message);
    }
}

public sealed class FailingSqlSnapshotStore(Configuration.Config config) : SqlSnapshotStore(config)
{
    public override void AroundPreRestart(Exception cause, object? message)
    {
        var isInitializationFailure = cause is ApplicationException { InnerException: not null }
                                      && cause.Message == "Failed to initialize SQL SnapshotStore.";
        foreach (var child in Context.GetChildren())
        {
            if (child.Path.Name.StartsWith("StreamSupervisor-", StringComparison.Ordinal))
                Context.ActorSelection("/user/lifetime-observer")
                    .Tell(new FailedInitSupervisor(child, isInitializationFailure, DateTimeOffset.UtcNow));
        }

        base.AroundPreRestart(cause, message);
    }
}
