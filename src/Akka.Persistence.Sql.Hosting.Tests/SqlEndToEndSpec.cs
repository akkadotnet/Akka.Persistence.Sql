// -----------------------------------------------------------------------
//  <copyright file="EndtoEndSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Sql.Tests.Common.Containers;
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Hosting.Tests
{
    public class SqlEndToEndSpec: Akka.Hosting.TestKit.TestKit, IClassFixture<SqliteContainer>
    {
        private const string GetAll = "getAll";
        private const string Ack = "ACK";
        private const string PId = "ac1";

        private readonly SqliteContainer _fixture;
        
        public SqlEndToEndSpec(ITestOutputHelper output, SqliteContainer fixture): base(nameof(SqlEndToEndSpec), output)
        {
            _fixture = fixture;
        }

        public sealed class MyPersistenceActor : ReceivePersistentActor
        {
            private List<int> _values = new ();

            public MyPersistenceActor(string persistenceId)
            {
                PersistenceId = persistenceId;
                
                Recover<SnapshotOffer>(offer =>
                {
                    if (offer.Snapshot is IEnumerable<int> ints)
                        _values = new List<int>(ints);
                });
                
                Recover<int>(_values.Add);
                
                Command<int>(i =>
                {
                    Persist(i, _ =>
                    {
                        _values.Add(i);
                        if (LastSequenceNr % 2 == 0)
                            SaveSnapshot(_values);
                        Sender.Tell(Ack);
                    });
                });

                Command<string>(str => str.Equals(GetAll), _ => Sender.Tell(_values.ToArray()));
                
                Command<SaveSnapshotSuccess>(_ => {});
            }

            public override string PersistenceId { get; }
        }

        protected override async Task BeforeTestStart()
        {
            await base.BeforeTestStart();
            await _fixture.InitializeAsync();
        }

        protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.WithSqlPersistence(
                connectionString: _fixture.ConnectionString,
                providerName: _fixture.ProviderName)
                .StartActors((system, registry) =>
                {
                    var myActor = system.ActorOf(Props.Create(() => new MyPersistenceActor(PId)));
                    registry.Register<MyPersistenceActor>(myActor);
                });
        }
        
        [Fact]
        public async Task Should_Start_ActorSystem_wth_Sql_Persistence()
        {
            var timeout = 3.Seconds();
            
            // arrange
            var myPersistentActor = ActorRegistry.Get<MyPersistenceActor>();
            
            // act
            (await myPersistentActor.Ask<string>(1, timeout)).Should().Be(Ack);
            (await myPersistentActor.Ask<string>(2, timeout)).Should().Be(Ack);
            var snapshot = await myPersistentActor.Ask<int[]>(GetAll, timeout);

            // assert
            snapshot.Should().BeEquivalentTo(new[] {1, 2});

            // kill + recreate actor with same PersistentId
            await myPersistentActor.GracefulStop(timeout);
            var myPersistentActor2 = Sys.ActorOf(Props.Create(() => new MyPersistenceActor(PId)));
            
            var snapshot2 = await myPersistentActor2.Ask<int[]>(GetAll, timeout);
            snapshot2.Should().BeEquivalentTo(new[] {1, 2});
            
            // validate configs
            var config = Sys.Settings.Config;
            config.GetString("akka.persistence.journal.plugin").Should().Be("akka.persistence.journal.sql");
            config.GetString("akka.persistence.snapshot-store.plugin").Should().Be("akka.persistence.snapshot-store.sql");
        }
    }
}
