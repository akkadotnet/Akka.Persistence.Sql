// -----------------------------------------------------------------------
//  <copyright file="SqlEndToEndSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Streams;
using Akka.Streams.TestKit;
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using LinqToDB;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Hosting.Tests
{
    public class SqlEndToEndSpec : SqlEndToEndSpecBase
    {
        public SqlEndToEndSpec(ITestOutputHelper output, SqliteContainer fixture)
            : base(nameof(SqlEndToEndSpec), output, fixture)
        {
        }

        protected override void ConfigureSqlPersistence(AkkaConfigurationBuilder builder)
        {
            builder.WithSqlPersistence(
                connectionString: Fixture.ConnectionString,
                providerName: Fixture.ProviderName);
        }
    }
    
    public class SqlDataOptionsEndToEndSpec : SqlEndToEndSpecBase
    {
        public SqlDataOptionsEndToEndSpec(ITestOutputHelper output, SqliteContainer fixture)
            : base(nameof(SqlDataOptionsEndToEndSpec), output, fixture)
        {
        }

        protected override void ConfigureSqlPersistence(AkkaConfigurationBuilder builder)
        {
            builder.WithSqlPersistence(new DataOptions()
                .UseConnectionString(Fixture.ProviderName, Fixture.ConnectionString));
        }
    }
    
    public abstract class SqlEndToEndSpecBase : Akka.Hosting.TestKit.TestKit, IClassFixture<SqliteContainer>
    {
        private const string GetAll = "getAll";
        private const string Ack = "ACK";
        private const string SnapshotAck = "SnapACK";
        private const string PId = "ac1";

        protected readonly SqliteContainer Fixture;

        protected SqlEndToEndSpecBase(string name, ITestOutputHelper output, SqliteContainer fixture) 
            : base(name, output, logLevel: LogLevel.Debug)
            => Fixture = fixture;

        protected override async Task BeforeTestStart()
        {
            await base.BeforeTestStart();
            await Fixture.InitializeAsync();
        }

        protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            ConfigureSqlPersistence(builder);
            
            builder
                .StartActors(
                    (system, registry) =>
                    {
                        var myActor = system.ActorOf(Props.Create(() => new MyPersistenceActor(PId)));
                        registry.Register<MyPersistenceActor>(myActor);
                    });
        }

        protected abstract void ConfigureSqlPersistence(AkkaConfigurationBuilder builder);

        [Fact]
        public async Task Should_Start_ActorSystem_wth_Sql_Persistence()
        {
            var timeout = 3.Seconds();

            // arrange
            var myPersistentActor = ActorRegistry.Get<MyPersistenceActor>();

            // act
            myPersistentActor.Tell(1);
            ExpectMsg<string>(Ack);
            myPersistentActor.Tell(2);
            ExpectMsg<string>(Ack);
            ExpectMsg<string>(SnapshotAck);
            var snapshot = await myPersistentActor.Ask<int[]>(GetAll, timeout);

            // assert
            snapshot.Should().BeEquivalentTo(new[] { 1, 2 });

            // kill + recreate actor with same PersistentId
            await myPersistentActor.GracefulStop(timeout);
            var myPersistentActor2 = Sys.ActorOf(Props.Create(() => new MyPersistenceActor(PId)));

            var snapshot2 = await myPersistentActor2.Ask<int[]>(GetAll, timeout);
            snapshot2.Should().BeEquivalentTo(new[] { 1, 2 });

            // validate configs
            var config = Sys.Settings.Config;
            config.GetString("akka.persistence.journal.plugin").Should().Be("akka.persistence.journal.sql");
            config.GetString("akka.persistence.snapshot-store.plugin").Should().Be("akka.persistence.snapshot-store.sql");

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
