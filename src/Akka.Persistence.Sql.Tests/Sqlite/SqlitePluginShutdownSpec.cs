using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Snapshot;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Sqlite;

public class SqlitePluginShutdownSpec : Akka.TestKit.Xunit.TestKit
{
    private static readonly Configuration.Config TestConfig = ConfigurationFactory.ParseString($$"""
        akka.actor.serialize-messages = on
        akka.persistence.journal {
            plugin = akka.persistence.journal.sql
            sql {
                class = "{{typeof(CleanupAwareJournal).AssemblyQualifiedName}}"
                auto-initialize = false
                provider-name = SQLite.MS
                connection-string = "Data Source=:memory:"
            }
        }
        akka.persistence.snapshot-store {
            plugin = akka.persistence.snapshot-store.sql
            sql {
                class = "{{typeof(CleanupAwareSnapshotStore).AssemblyQualifiedName}}"
                auto-initialize = false
                provider-name = SQLite.MS
                connection-string = "Data Source=:memory:"
            }
        }
        """).WithFallback(SqlPersistence.DefaultConfiguration);

    public SqlitePluginShutdownSpec(ITestOutputHelper output) : base(TestConfig.ToString(), output) { }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Initialization_completes_with_message_serialization_enabled(bool snapshot)
    {
        Assert.True(Sys.Settings.SerializeAllMessages);
        var plugin = Plugin(snapshot);
        var resources = await plugin.Ask<CleanupResources>(GetCleanupResources.Instance, TimeSpan.FromSeconds(5));
        Assert.False(resources.Shutdown.IsCancellationRequested);
    }

    [Fact]
    public async Task Journal_disposes_its_token_source_when_a_cancellation_callback_throws()
    {
        var journal = Plugin(snapshot: false);
        var resources = await journal.Ask<CleanupResources>(GetCleanupResources.Instance, TimeSpan.FromSeconds(5));
        resources.Shutdown.Token.Register(ThrowOnCancellation);

        await StopAndWatch(journal);

        AssertDisposed(resources.Shutdown);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Snapshot_disposes_both_token_sources_when_cleanup_callbacks_throw(bool actorThrows, bool daoThrows)
    {
        var store = Plugin(snapshot: true);
        var resources = await store.Ask<CleanupResources>(GetCleanupResources.Instance, TimeSpan.FromSeconds(5));
        Assert.NotNull(resources.DaoShutdown);
        if (actorThrows)
            resources.Shutdown.Token.Register(ThrowOnCancellation);
        if (daoThrows)
            resources.DaoShutdown.Token.Register(ThrowOnCancellation);

        await StopAndWatch(store);

        AssertDisposed(resources.Shutdown);
        AssertDisposed(resources.DaoShutdown);
    }

    private IActorRef Plugin(bool snapshot)
        => snapshot
            ? Persistence.Instance.Apply(Sys).SnapshotStoreFor(null)
            : Persistence.Instance.Apply(Sys).JournalFor(null);

    private async Task StopAndWatch(IActorRef actor)
    {
        var probe = CreateTestProbe();
        probe.Watch(actor);
        Sys.Stop(actor);
        await probe.ExpectTerminatedAsync(actor, TimeSpan.FromSeconds(5));
    }

    private static void AssertDisposed(CancellationTokenSource source)
    {
        Assert.True(source.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => source.Token);
    }

    private static void ThrowOnCancellation() => throw new InvalidOperationException("Injected cleanup callback failure");

    // Fault injection at the actual cancellation boundary, without adding production test hooks.
    internal static T Field<T>(object instance, Type declaringType, string name)
    {
        var field = declaringType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field.GetValue(instance));
    }
}

internal sealed class GetCleanupResources : INoSerializationVerificationNeeded
{
    public static readonly GetCleanupResources Instance = new();
}

internal sealed record CleanupResources(CancellationTokenSource Shutdown, CancellationTokenSource? DaoShutdown)
    : INoSerializationVerificationNeeded;

public sealed class CleanupAwareJournal(Configuration.Config config) : SqlWriteJournal(config)
{
    protected override bool ReceivePluginInternal(object message)
    {
        if (message is not GetCleanupResources)
            return base.ReceivePluginInternal(message);

        Sender.Tell(new CleanupResources(
            SqlitePluginShutdownSpec.Field<CancellationTokenSource>(this, typeof(SqlWriteJournal), "_pendingWriteCts"), null));
        return true;
    }
}

public sealed class CleanupAwareSnapshotStore(Configuration.Config config) : SqlSnapshotStore(config)
{
    protected override bool ReceivePluginInternal(object message)
    {
        if (message is not GetCleanupResources)
            return base.ReceivePluginInternal(message);

        var dao = SqlitePluginShutdownSpec.Field<ByteArraySnapshotDao>(this, typeof(SqlSnapshotStore), "_dao");
        Sender.Tell(new CleanupResources(
            SqlitePluginShutdownSpec.Field<CancellationTokenSource>(this, typeof(SqlSnapshotStore), "_initializationCts"),
            SqlitePluginShutdownSpec.Field<CancellationTokenSource>(dao, typeof(ByteArraySnapshotDao), "_shutdownCts")));
        return true;
    }
}
