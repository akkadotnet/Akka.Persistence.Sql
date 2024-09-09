// -----------------------------------------------------------------------
//  <copyright file="SqlDataOptionsEndToEndSpecBase.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Configuration;
using Akka.Event;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Streams;
using Akka.Streams.TestKit;
using Akka.TestKit;
using Akka.TestKit.Xunit2;
using Akka.TestKit.Xunit2.Internals;
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

#nullable enable
namespace Akka.Persistence.Sql.Tests
{
    public abstract class SqlDataOptionsEndToEndSpecBase<TContainer> : 
        TestKitBase,
        IClassFixture<TContainer>,
        IAsyncLifetime 
        where TContainer: class, ITestContainer
    {
        private static Configuration.Config Config() =>  ConfigurationFactory.ParseString(
"""
akka.persistence {
    journal {
        plugin = "akka.persistence.journal.sql"
    }
    snapshot-store {
        plugin = "akka.persistence.snapshot-store.sql"
    }
}
""")
            .WithFallback(SqlPersistence.DefaultConfiguration);

        private const string GetAll = "getAll";
        private const string Ack = "ACK";
        private const string SnapshotAck = "SnapACK";
        private const string PId = "ac1";

        private readonly string _name;
        private readonly DataOptions _dataOptions;
        private readonly ITestOutputHelper? _output;
        private readonly TContainer _fixture;
        private IActorRef? _persistenceActor;

        protected SqlDataOptionsEndToEndSpecBase(string name, ITestOutputHelper? output, TContainer fixture) : base(new XunitAssertions(), null, name)
        {
            _name = name;
            _dataOptions = new DataOptions()
                .UseConnectionString(fixture.ProviderName, fixture.ConnectionString);
            _fixture = fixture;
            _output = output;
        }

        public async Task InitializeAsync()
        {
            await _fixture.InitializeAsync();
            
            var dataOptionsSetup = new MultiDataOptionsSetup();
            dataOptionsSetup.AddDataOptions("akka.persistence.journal.sql", _dataOptions);
            dataOptionsSetup.AddDataOptions("akka.persistence.query.journal.sql", _dataOptions);
            dataOptionsSetup.AddDataOptions("akka.persistence.snapshot-store.sql", _dataOptions);

            var setup = ActorSystemSetup.Create(
                BootstrapSetup.Create().WithConfig(Config()),
                dataOptionsSetup);
            base.InitializeTest(null, setup, _name, null);
            InitializeLogger(Sys);
            
            _persistenceActor = Sys.ActorOf(Props.Create(() => new MyPersistenceActor(PId)), "persistence-actor-1");
        }

        public async Task DisposeAsync()
        {
            await Sys.Terminate();
            await _fixture.DisposeAsync();
        }

        /// <summary>
        /// Initializes a new <see cref="TestOutputLogger"/> used to log messages.
        /// </summary>
        /// <param name="system">The actor system used to attach the logger</param>
        private void InitializeLogger(ActorSystem system)
        {
            if (_output is null)
                return;

            var extSystem = (ExtendedActorSystem)system;
            var logger = extSystem.SystemActorOf(Props.Create(() => new TestOutputLogger(_output)), "log-test");
            logger.Ask<LoggerInitialized>(new InitializeLogger(system.EventStream), TimeSpan.FromSeconds(3))
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }
        
        protected override void InitializeTest(ActorSystem system, ActorSystemSetup config, string actorSystemName, string testActorName)
        {
            // no-op, call after database are set up
        }

        [Fact]
        public async Task Should_Start_ActorSystem_wth_Sql_Persistence()
        {
            var timeout = 3.Seconds();

            // act
            _persistenceActor.Tell(1);
            ExpectMsg<string>(Ack);
            _persistenceActor.Tell(2);
            ExpectMsg<string>(Ack);
            ExpectMsg<string>(SnapshotAck);
            var snapshot = await _persistenceActor.Ask<int[]>(GetAll, timeout);

            // assert
            snapshot.Should().BeEquivalentTo(new[] { 1, 2 });

            // kill + recreate actor with same PersistentId
            await _persistenceActor.GracefulStop(timeout);
            var myPersistentActor2 = Sys.ActorOf(Props.Create(() => new MyPersistenceActor(PId)), "persistence-actor-2");

            var snapshot2 = await myPersistentActor2.Ask<int[]>(GetAll, timeout);
            snapshot2.Should().BeEquivalentTo(new[] { 1, 2 });

            // validate configs
            var config = Sys.Settings.Config;
            
            var journalConfig = config.GetConfig("akka.persistence.journal");
            journalConfig.GetString("plugin").Should().Be("akka.persistence.journal.sql");
            journalConfig.GetString("sql.connection-string").Should().Be(string.Empty);
            journalConfig.GetString("sql.provider-name").Should().Be(string.Empty);
            
            var snapshotConfig = config.GetConfig("akka.persistence.snapshot-store");
            snapshotConfig.GetString("plugin").Should().Be("akka.persistence.snapshot-store.sql");
            snapshotConfig.GetString("sql.connection-string").Should().Be(string.Empty);
            snapshotConfig.GetString("sql.provider-name").Should().Be(string.Empty);

            // validate that query is working
            var readJournal = Sys.ReadJournalFor<SqlReadJournal>("akka.persistence.query.journal.sql");
            var source = readJournal.AllEvents(Offset.NoOffset());
            var probe = source.RunWith(this.SinkProbe<EventEnvelope>(), Sys.Materializer());
            probe.Request(2);
            probe.ExpectNext<EventEnvelope>(p => p.PersistenceId == PId && p.SequenceNr == 1L && p.Event.Equals(1));
            probe.ExpectNext<EventEnvelope>(p => p.PersistenceId == PId && p.SequenceNr == 2L && p.Event.Equals(2));
            await probe.CancelAsync();
        }

        private sealed class MyPersistenceActor : ReceivePersistentActor
        {
            private List<int> _values = new();
            private IActorRef? _sender;

            public MyPersistenceActor(string persistenceId)
            {
                PersistenceId = persistenceId;

                Recover<SnapshotOffer>(
                    offer =>
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
