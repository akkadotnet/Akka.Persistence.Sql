// -----------------------------------------------------------------------
//  <copyright file="Issue432SpecBase.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Actor.Setup;
using Akka.Event;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Internal;
using Akka.TestKit;
using Akka.TestKit.Xunit2;
using Akka.TestKit.Xunit2.Internals;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests
{
    // Reproduction spec for https://github.com/akkadotnet/Akka.Persistence.Sql/issues/432
    public abstract class Issue432SpecBase<TContainer>: 
        TestKitBase,
        IClassFixture<TContainer>,
        IAsyncLifetime 
        where TContainer: class, ITestContainer
    {
        private const int RepeatCount = 200;
        private const string PId = "ac1";
        
        private static string Config(ITestContainer fixture) => 
            $$"""
              akka.persistence {
                  journal {
                      plugin = "akka.persistence.journal.sql"
                      sql {
                          class = "Akka.Persistence.Sql.Journal.SqlWriteJournal, Akka.Persistence.Sql"
                          connection-string = "{{fixture.ConnectionString}}"
                          provider-name = "{{fixture.ProviderName}}"
                      }
                  }
                  query.journal.sql {
                      class = "Akka.Persistence.Sql.Query.SqlReadJournalProvider, Akka.Persistence.Sql"
                      connection-string = "{{fixture.ConnectionString}}"
                      provider-name = "{{fixture.ProviderName}}"
                  }
                  snapshot-store {
                      plugin = "akka.persistence.snapshot-store.sql"
                      sql {
                          class = "Akka.Persistence.Sql.Snapshot.SqlSnapshotStore, Akka.Persistence.Sql"
                          connection-string = "{{fixture.ConnectionString}}"
                          provider-name = "{{fixture.ProviderName}}"
                      }
                  }
              }
              """;

        private readonly ITestOutputHelper? _output;
        private readonly TContainer _fixture;
        private readonly string _actorSystemName;

        protected Issue432SpecBase(string name, TContainer fixture, ITestOutputHelper? output = null)
            : base(new XunitAssertions(), null, name)
        {
            _fixture = fixture;
            _actorSystemName = name;
            _output = output;
        }
        
        #region Setup

        public async Task InitializeAsync()
        {
            await _fixture.InitializeAsync();
            
            var setup = ActorSystemSetup.Create(BootstrapSetup.Create().WithConfig(Config(_fixture)));
            base.InitializeTest(null, setup, _actorSystemName, "test-actor");
            InitializeLogger(Sys);
        }

        public async Task DisposeAsync()
        {
            await Sys.Terminate();
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
        #endregion

        [Theory(DisplayName = "Rapid multiple SaveSnapshot invocation with no journal persist should only save the latest snapshot")]
        [Repeat(RepeatCount)]
        public async Task MultipleSnapshotsWithNoPersistTest(int iteration)
        {
            var persistenceActor = CreatePersistenceActor(Sys);
                
            // No persist call before SaveSnapshot burst
            persistenceActor.Tell(new TakeSnapshotValues([[0], [1], [2], [3], ]), TestActor);
            await ExpectMsgAsync<SnapshotAck>();
                
            await StopActorAsync(persistenceActor);
            persistenceActor = CreatePersistenceActor(Sys);
                
            persistenceActor.Tell(GetAll.Instance, TestActor);
            var result = await ExpectMsgAsync<int[]>();
            await StopActorAsync(persistenceActor);
                
            result.Length.Should().Be(1, $"expecting an array with length 1 (on iteration {iteration}/{RepeatCount})");
            result[0].Should().Be(3, $"recovered snapshot should be the last snapshot (on iteration {iteration}/{RepeatCount})");
        }

        [Theory(DisplayName = "Rapid multiple SaveSnapshot invocation with journal persist should only save the latest snapshot")]
        [Repeat(RepeatCount)]
        public async Task MultipleSnapshotsWithPersistTest(int iteration)
        {
            var persistenceActor = CreatePersistenceActor(Sys);
                
            // persist 2 events
            persistenceActor.Tell(1, TestActor);
            ExpectMsg<Ack>();
            persistenceActor.Tell(2, TestActor);
            ExpectMsg<Ack>();
                
            persistenceActor.Tell(new TakeSnapshotValues([[0], [1], [2], [3], ]), TestActor);
            await ExpectMsgAsync<SnapshotAck>();
                
            await StopActorAsync(persistenceActor);
            persistenceActor = CreatePersistenceActor(Sys);
                
            persistenceActor.Tell(GetAll.Instance, TestActor);
            var result = await ExpectMsgAsync<int[]>();
            await StopActorAsync(persistenceActor);
                
            result.Length.Should().Be(1, $"expecting an array with length 1 (on iteration {iteration}/{RepeatCount})");
            result[0].Should().Be(3, $"recovered snapshot should be the last snapshot (on iteration {iteration}/{RepeatCount})");
        }
        
        [Fact(DisplayName = "Multiple SaveSnapshot invocation with the same sequence number should not throw")]
        public async Task MultipleSnapshotsWithSameSeqNo()
        {
            var persistence = Persistence.Instance.Apply(Sys);
            var snapshotStore = persistence.SnapshotStoreFor(null);
            
            var metadata = new SnapshotMetadata(PId, 3, DateTime.Now);
            snapshotStore.Tell(new SaveSnapshot(metadata, 2), TestActor);
            var success = await ExpectMsgAsync<SaveSnapshotSuccess>();
            success.Metadata.Should().Be(metadata);
            
            metadata = new SnapshotMetadata(PId, 3, DateTime.Now);
            snapshotStore.Tell(new SaveSnapshot(metadata, 3), TestActor);
            success = await ExpectMsgAsync<SaveSnapshotSuccess>();
            success.Metadata.Should().Be(metadata);
        }

        #region Utility
        private static IActorRef CreatePersistenceActor(ActorSystem sys)
            => sys.ActorOf(Props.Create(() => new MyPersistenceActor(PId)), "persistence-actor-1");

        private async Task StopActorAsync(IActorRef actor)
        {
            await WatchAsync(actor);
            actor.Tell(PoisonPill.Instance);
            await ExpectTerminatedAsync(actor);
            await UnwatchAsync(actor);
        }
        #endregion
        

        #region Classes
        private sealed class MyPersistenceActor : ReceivePersistentActor
        {
            private List<int> _values = new();
            private IActorRef? _sender;
            private int _snapshotCount;
            private int _savedSnapshotCount;

            public MyPersistenceActor(string persistenceId)
            {
                PersistenceId = persistenceId;

                Recover<SnapshotOffer>(
                    offer =>
                    {
                        if (offer.Snapshot is IEnumerable<int> ints)
                            _values = ints.ToList();
                    });
                
                Recover<int>(_values.Add);
                
                Command<int>( i =>
                {
                    _sender = Sender;
                    Persist(i, _ =>
                        {
                            _values.Add(i);
                            _sender.Tell(Ack.Instance);
                        });
                });
                
                Command<TakeSnapshot>(_ => SaveSnapshot(_values));
                
                Command<TakeSnapshotValue>(msg => SaveSnapshots([msg.Values]));
                
                Command<TakeSnapshotValues>(msg => SaveSnapshots(msg.Values));
                
                Command<GetAll>(_ => Sender.Tell(_values.ToArray()));
                
                Command<SaveSnapshotSuccess>(
                    _ =>
                    {
                        _savedSnapshotCount++;
                        if(_savedSnapshotCount == _snapshotCount)
                            _sender.Tell(SnapshotAck.Instance);
                    });
            }

            public override string PersistenceId { get; }

            private void SaveSnapshots(int[][] snapshots)
            {
                _sender = Sender;
                _snapshotCount = snapshots.Length;
                _savedSnapshotCount = 0;
                foreach (var snapshot in snapshots)
                {
                    _values = snapshot.ToList();
                    SaveSnapshot(_values);
                }
            }
        }
        
        private sealed class Ack
        {
            public static readonly Ack Instance = new Ack();
            private Ack() { }
        }
        
        private sealed class SnapshotAck
        {
            public static readonly SnapshotAck Instance = new();
            private SnapshotAck() { }
        }

        private sealed class GetAll
        {
            public static readonly GetAll Instance = new();
            private GetAll() { }
        }
        
        private sealed class TakeSnapshot
        {
            public static readonly TakeSnapshot Instance = new();
            private TakeSnapshot() { }
        }

        private sealed record TakeSnapshotValue(int[] Values);
        
        private sealed record TakeSnapshotValues(int[][] Values);
        
        #endregion
    }
}
