// -----------------------------------------------------------------------
//  <copyright file="SourceGeneratedSerializerSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Serialization.V2;
using FluentAssertions;
using Xunit;

#nullable enable
namespace Akka.Persistence.Sql.Tests.Sqlite
{
    public interface ISourceGeneratedMarker
    {
    }

    [AkkaSerializable(Manifest = SourceGeneratedSerializerSpec.EventManifest)]
    public sealed record SourceGeneratedEvent(
        [property: AkkaField(0)] int Value) : ISourceGeneratedMarker;

    [AkkaSerializable(Manifest = SourceGeneratedSerializerSpec.SnapshotManifest)]
    public sealed record SourceGeneratedSnapshot(
        [property: AkkaField(0)] int Sum) : ISourceGeneratedMarker;

    [AkkaSerializer<ISourceGeneratedMarker>("source-generated-test", 7_654_321)]
    public sealed partial class SourceGeneratedTestSerializer : AkkaSerializer
    {
        public static partial SerializerRegistration CreateRegistration();
    }

    /// <summary>
    ///     Regression test for akkadotnet/akka.net#8784. Under Akka.NET 1.6.0-beta2 a source-generated
    ///     serializer was not a <see cref="Akka.Serialization.SerializerWithStringManifest" />, so the plugin
    ///     stored the CLR type name as manifest and the serializer could not read its own rows back.
    /// </summary>
    public sealed class SourceGeneratedSerializerSpec
    {
        public const string EventManifest = "explicit-manifest-v1";
        public const string SnapshotManifest = "explicit-snapshot-manifest-v1";

        private const string PersistenceId = "source-generated-1";

        [Fact(DisplayName = "Should_RecoverEventsAndSnapshots_When_SourceGeneratedSerializerStoresExplicitManifest")]
        public async Task Should_RecoverEventsAndSnapshots_When_SourceGeneratedSerializerStoresExplicitManifest()
        {
            using var db = new SqliteContainer();
            await db.InitializeAsync();
            var timeout = TimeSpan.FromSeconds(10);

            // first system: three events and a snapshot at sequence number 3, then one more event
            var sys1 = ActorSystem.Create("sg-1", CreateConfig(db));
            try
            {
                var actor = sys1.ActorOf(Props.Create(() => new SumActor(PersistenceId)));
                (await actor.Ask<int>(new Add(1), timeout)).Should().Be(1);
                (await actor.Ask<int>(new Add(2), timeout)).Should().Be(3);
                (await actor.Ask<int>(new Add(3), timeout)).Should().Be(6);
                (await actor.Ask<SaveSnapshotSuccess>(TakeSnapshot.Instance, timeout)).Metadata.SequenceNr.Should().Be(3);
                (await actor.Ask<int>(new Add(4), timeout)).Should().Be(10);
            }
            finally
            {
                await sys1.Terminate();
            }

            // the stored manifests are the serializers' own, not CLR type names
            ReadManifests(db, "journal").Should().Equal(EventManifest, EventManifest, EventManifest, EventManifest);
            ReadManifests(db, "snapshot").Should().Equal(SnapshotManifest);

            // second system: recover from the snapshot plus the replayed event, then persist again
            var sys2 = ActorSystem.Create("sg-2", CreateConfig(db));
            try
            {
                var actor = sys2.ActorOf(Props.Create(() => new SumActor(PersistenceId)));
                (await actor.Ask<int>(GetSum.Instance, timeout)).Should().Be(10);
                (await actor.Ask<int>(new Add(5), timeout)).Should().Be(15);
            }
            finally
            {
                await sys2.Terminate();
            }

            // third system: everything written by the second system is recoverable too
            var sys3 = ActorSystem.Create("sg-3", CreateConfig(db));
            try
            {
                var actor = sys3.ActorOf(Props.Create(() => new SumActor(PersistenceId)));
                (await actor.Ask<int>(GetSum.Instance, timeout)).Should().Be(15);
            }
            finally
            {
                await sys3.Terminate();
            }

            ReadManifests(db, "journal").Should().HaveCount(5).And.AllBe(EventManifest);
        }

        private static string[] ReadManifests(SqliteContainer db, string table)
        {
            using var connection = new SQLiteConnection(db.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = table == "journal"
                ? "SELECT manifest FROM journal ORDER BY sequence_number"
                : "SELECT manifest FROM snapshot ORDER BY sequence_number";
            using var reader = command.ExecuteReader();

            var manifests = new System.Collections.Generic.List<string>();
            while (reader.Read())
                manifests.Add(reader.GetString(0));
            return manifests.ToArray();
        }

        private static Akka.Configuration.Config CreateConfig(SqliteContainer db)
        {
            var markerType = typeof(ISourceGeneratedMarker);
            var serializerType = typeof(SourceGeneratedTestSerializer);

            return ConfigurationFactory.ParseString(
$$"""
akka.actor {
    serializers.source-generated-test = "{{serializerType.FullName}}, {{serializerType.Assembly.GetName().Name}}"
    serialization-bindings {
        "{{markerType.FullName}}, {{markerType.Assembly.GetName().Name}}" = source-generated-test
    }
}
akka.persistence {
    journal {
        plugin = "akka.persistence.journal.sql"
        sql {
            class = "Akka.Persistence.Sql.Journal.SqlWriteJournal, Akka.Persistence.Sql"
            connection-string = "{{db.ConnectionString}}"
            provider-name = "{{db.ProviderName}}"
        }
    }
    snapshot-store {
        plugin = "akka.persistence.snapshot-store.sql"
        sql {
            class = "Akka.Persistence.Sql.Snapshot.SqlSnapshotStore, Akka.Persistence.Sql"
            connection-string = "{{db.ConnectionString}}"
            provider-name = "{{db.ProviderName}}"
        }
    }
}
""");
        }

        private sealed record Add(int Value);

        private sealed class TakeSnapshot
        {
            public static readonly TakeSnapshot Instance = new();
        }

        private sealed class GetSum
        {
            public static readonly GetSum Instance = new();
        }

        private sealed class SumActor : ReceivePersistentActor
        {
            private int _sum;
            private IActorRef _snapshotRequester = ActorRefs.Nobody;

            public SumActor(string persistenceId)
            {
                PersistenceId = persistenceId;

                Recover<SnapshotOffer>(offer => _sum = ((SourceGeneratedSnapshot)offer.Snapshot).Sum);
                Recover<SourceGeneratedEvent>(e => _sum += e.Value);

                Command<Add>(add =>
                {
                    var sender = Sender;
                    Persist(new SourceGeneratedEvent(add.Value), e =>
                    {
                        _sum += e.Value;
                        sender.Tell(_sum);
                    });
                });
                Command<TakeSnapshot>(_ =>
                {
                    _snapshotRequester = Sender;
                    SaveSnapshot(new SourceGeneratedSnapshot(_sum));
                });
                Command<SaveSnapshotSuccess>(success => _snapshotRequester.Tell(success));
                Command<GetSum>(_ => Sender.Tell(_sum));
            }

            public override string PersistenceId { get; }
        }
    }
}
