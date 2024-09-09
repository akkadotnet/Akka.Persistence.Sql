// -----------------------------------------------------------------------
//  <copyright file="SqlEndToEndSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Data.SQLite;
using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Streams;
using Akka.Streams.TestKit;
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Hosting.Tests
{
    public class CustomSqlDataOptionsEndToEndSpec : Akka.Hosting.TestKit.TestKit, IClassFixture<SqliteContainer>
    {
        private const string GetAll = "getAll";
        private const string Ack = "ACK";
        private const string SnapshotAck = "SnapACK";
        private const string PId = "ac1";

        private readonly SqliteContainer _fixture;
        private readonly DataOptions _dataOptions;

        public CustomSqlDataOptionsEndToEndSpec(ITestOutputHelper output, SqliteContainer fixture) : base(nameof(SqlEndToEndSpec), output)
        {
            _fixture = fixture;
            _dataOptions = new DataOptions().UseConnectionString(_fixture.ProviderName, _fixture.ConnectionString);
        }

        protected override async Task BeforeTestStart()
        {
            await base.BeforeTestStart();
            await _fixture.InitializeAsync();
        }

        protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder
                .WithSqlPersistence(_dataOptions)
                .WithSqlPersistence(
                    journal =>
                    {
                        journal.IsDefaultPlugin = false;
                        journal.Identifier = "custom";
                        journal.DatabaseOptions = JournalDatabaseOptions.Default;
                        journal.DatabaseOptions.JournalTable!.TableName = "journal2";
                        journal.DatabaseOptions.MetadataTable!.TableName = "journal_metadata2";
                        journal.DatabaseOptions.TagTable!.TableName = "tags2";
                        journal.DataOptions = _dataOptions;
                    },
                    snapshot =>
                    {
                        snapshot.IsDefaultPlugin = false;
                        snapshot.Identifier = "custom";
                        snapshot.DatabaseOptions = SnapshotDatabaseOptions.Default;
                        snapshot.DatabaseOptions.SnapshotTable!.TableName = "snapshot2";
                        snapshot.DataOptions = _dataOptions;
                    })
                .StartActors(
                    (system, registry) =>
                    {
                        var myActor = system.ActorOf(Props.Create(() => new MyPersistenceActor(PId)), "default");
                        registry.Register<MyPersistenceActor>(myActor);
                        
                        myActor = system.ActorOf(Props.Create(() => new MyCustomPersistenceActor(PId)), "custom");
                        registry.Register<MyCustomPersistenceActor>(myActor);
                    });
        }

        [Fact]
        public async Task Should_Start_ActorSystem_wth_Sql_Persistence()
        {
            var timeout = 3.Seconds();

            #region Default SQL plugin
            
            // arrange
            var myPersistentActor = await ActorRegistry.GetAsync<MyPersistenceActor>();

            // act
            myPersistentActor.Tell(1, TestActor);
            ExpectMsg<string>(Ack);
            myPersistentActor.Tell(2, TestActor);
            ExpectMsg<string>(Ack);
            ExpectMsg<string>(SnapshotAck);
            var snapshot = await myPersistentActor.Ask<int[]>(GetAll, timeout);

            // assert
            snapshot.Should().BeEquivalentTo(new[] { 1, 2 });
            
            #endregion

            #region Custom SQL plugin
            
            // arrange
            var customMyPersistentActor = await ActorRegistry.GetAsync<MyCustomPersistenceActor>();

            // act
            customMyPersistentActor.Tell(1, TestActor);
            ExpectMsg<string>(Ack);
            customMyPersistentActor.Tell(2, TestActor);
            ExpectMsg<string>(Ack);
            ExpectMsg<string>(SnapshotAck);
            var customSnapshot = await customMyPersistentActor.Ask<int[]>(GetAll, timeout);

            // assert
            customSnapshot.Should().BeEquivalentTo(new[] { 1, 2 });
            
            #endregion
            
            
            // kill + recreate actor with same PersistentId
            await myPersistentActor.GracefulStop(timeout);
            var myPersistentActor2 = Sys.ActorOf(Props.Create(() => new MyPersistenceActor(PId)));

            var snapshot2 = await myPersistentActor2.Ask<int[]>(GetAll, timeout);
            snapshot2.Should().BeEquivalentTo(new[] { 1, 2 });

            await customMyPersistentActor.GracefulStop(timeout);
            var customMyPersistentActor2 = Sys.ActorOf(Props.Create(() => new MyCustomPersistenceActor(PId)));

            var customSnapshot2 = await customMyPersistentActor2.Ask<int[]>(GetAll, timeout);
            customSnapshot2.Should().BeEquivalentTo(new[] { 1, 2 });
            
            // validate configs
            var config = Sys.Settings.Config;
            config.GetString("akka.persistence.journal.plugin").Should().Be("akka.persistence.journal.sql");
            config.GetString("akka.persistence.snapshot-store.plugin").Should().Be("akka.persistence.snapshot-store.sql");

            var setupOption = Sys.Settings.Setup.Get<MultiDataOptionsSetup>();
            setupOption.HasValue.Should().BeTrue();
            var setup = setupOption.Value;
            
            var customJournalConfig = config.GetConfig("akka.persistence.journal.custom");
            customJournalConfig.Should().NotBeNull();
            customJournalConfig.GetString("connection-string").Should().Be(string.Empty);
            customJournalConfig.GetString("provider-name").Should().Be(string.Empty);

            setup.TryGetDataOptionsFor("akka.persistence.journal.custom", out var journalDataOptions).Should().BeTrue();
            journalDataOptions.Should().Be(_dataOptions);

            var customSnapshotConfig = config.GetConfig("akka.persistence.snapshot-store.custom");
            customSnapshotConfig.Should().NotBeNull();
            customSnapshotConfig.GetString("connection-string").Should().Be(string.Empty);
            customSnapshotConfig.GetString("provider-name").Should().Be(string.Empty);

            setup.TryGetDataOptionsFor("akka.persistence.snapshot-store.custom", out var snapshotDataOptions).Should().BeTrue();
            snapshotDataOptions.Should().Be(_dataOptions);

            // validate that query is working
            var readJournal = Sys.ReadJournalFor<SqlReadJournal>("akka.persistence.query.journal.sql");
            var source = readJournal.AllEvents(Offset.NoOffset());
            var probe = source.RunWith(this.SinkProbe<EventEnvelope>(), Sys.Materializer());
            probe.Request(2);
            probe.ExpectNext<EventEnvelope>(p => p.PersistenceId == PId && p.SequenceNr == 1L && p.Event.Equals(1));
            probe.ExpectNext<EventEnvelope>(p => p.PersistenceId == PId && p.SequenceNr == 2L && p.Event.Equals(2));
            await probe.CancelAsync();

            var customReadJournal = Sys.ReadJournalFor<SqlReadJournal>("akka.persistence.query.journal.custom");
            var customSource = customReadJournal.AllEvents(Offset.NoOffset());
            var customProbe = customSource.RunWith(this.SinkProbe<EventEnvelope>(), Sys.Materializer());
            customProbe.Request(2);
            customProbe.ExpectNext<EventEnvelope>(p => p.PersistenceId == PId && p.SequenceNr == 1L && p.Event.Equals(1));
            customProbe.ExpectNext<EventEnvelope>(p => p.PersistenceId == PId && p.SequenceNr == 2L && p.Event.Equals(2));
            await customProbe.CancelAsync();

            // Probe the database directly to make sure that all tables were created properly
            var tables = await GetTableNamesAsync(_fixture.ConnectionString);

            tables.Should().Contain("journal");
            tables.Should().Contain("tags");
            tables.Should().Contain("snapshot");
            tables.Should().Contain("journal2");
            tables.Should().Contain("tags2");
            tables.Should().Contain("snapshot2");
        }

        private static async Task<string[]> GetTableNamesAsync(string connectionString)
        {
            await using var conn = new SQLiteConnection(connectionString);
            await conn.OpenAsync();
            
            var cmd = new SQLiteCommand("SELECT name FROM sqlite_schema WHERE type = 'table' AND name NOT LIKE 'sqlite_%'", conn);
            var reader = await cmd.ExecuteReaderAsync();
            var tables = new List<string>();
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
            await reader.CloseAsync();
            
            return tables.ToArray();
        }

        private sealed class MyPersistenceActor : ReceivePersistentActor
        {
            private List<int> _values = new();
            private IActorRef? _sender;

            public MyPersistenceActor(string persistenceId)
            {
                PersistenceId = persistenceId;
                JournalPluginId = "akka.persistence.journal.sql";
                SnapshotPluginId = "akka.persistence.snapshot-store.sql";

                Recover<SnapshotOffer>(offer =>
                {
                    if (offer.Snapshot is IEnumerable<int> ints)
                        _values = new List<int>(ints);
                });

                Recover<int>(_values.Add);

                Command<int>( i =>
                {
                    _sender = Sender;
                    Persist(
                        i,
                        _ =>
                        {
                            _values.Add(i);
                            if (LastSequenceNr % 2 == 0)
                                SaveSnapshot(_values);
                            _sender.Tell(Ack);
                        });
                });

                Command<string>(str => str.Equals(GetAll), _ => Sender.Tell(_values.ToArray()));

                Command<SaveSnapshotSuccess>(_ => _sender.Tell(SnapshotAck));
            }

            public override string PersistenceId { get; }
        }
        
        private sealed class MyCustomPersistenceActor : ReceivePersistentActor
        {
            private List<int> _values = new();
            private IActorRef? _sender;

            public MyCustomPersistenceActor(string persistenceId)
            {
                PersistenceId = persistenceId;
                JournalPluginId = "akka.persistence.journal.custom";
                SnapshotPluginId = "akka.persistence.snapshot-store.custom";

                Recover<SnapshotOffer>(offer =>
                {
                    if (offer.Snapshot is IEnumerable<int> ints)
                        _values = new List<int>(ints);
                });

                Recover<int>(_values.Add);

                Command<int>( i =>
                {
                    _sender = Sender;
                    Persist(
                        i,
                        _ =>
                        {
                            _values.Add(i);
                            if (LastSequenceNr % 2 == 0)
                                SaveSnapshot(_values);
                            _sender.Tell(Ack);
                        });
                });

                Command<string>(str => str.Equals(GetAll), _ => Sender.Tell(_values.ToArray()));

                Command<SaveSnapshotSuccess>(_ => _sender.Tell(SnapshotAck));
            }

            public override string PersistenceId { get; }
        }
    }
}
