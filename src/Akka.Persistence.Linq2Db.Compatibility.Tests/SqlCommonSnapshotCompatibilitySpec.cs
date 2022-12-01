using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.TestKit;
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public abstract class SqlCommonSnapshotCompatibilitySpec: IAsyncLifetime
    {
        private Akka.TestKit.Xunit2.TestKit _testKit;
        private ActorSystem _sys;
        private TestProbe _probe;
        private ILoggingAdapter _log;
        private readonly ITestOutputHelper _helper;
        
        protected abstract Configuration.Config Config { get; }
        public SqlCommonSnapshotCompatibilitySpec(ITestOutputHelper helper)
        {
            _helper = helper;
        }

        protected abstract string OldSnapshot { get; }
        protected abstract string NewSnapshot { get; }
        
        public Task InitializeAsync()
        {
            _testKit = new Akka.TestKit.Xunit2.TestKit(Config, nameof(SqlCommonJournalCompatibilitySpec), _helper);
            _sys = _testKit.Sys;
            _probe = _testKit.CreateTestProbe();
            _log = _testKit.Log;
            return Task.CompletedTask;
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
        
        [Fact]
        public async Task Can_Recover_SqlCommon_Snapshot()
        {
            var persistRef = _sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(OldSnapshot, "p-1")), "test-snap-recover-1");
            var ourGuid = Guid.NewGuid();
            
            (await persistRef.Ask<bool>(new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 }))
                .Should().BeTrue();
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            EnsureTerminated(persistRef);
            
            persistRef =  _sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(NewSnapshot, "p-1")), "test-snap-recover-1");
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
        }
        
        [Fact]
        public async Task Can_Persist_SqlCommon_Snapshot()
        {
            var persistRef = _sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(OldSnapshot, "p-2")), "test-snap-persist-1");
            var ourGuid = Guid.NewGuid();
            
            (await persistRef.Ask<bool>(new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 }))
                .Should().BeTrue();
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            EnsureTerminated(persistRef);
            
            persistRef = _sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(NewSnapshot, "p-2")), "test-snap-persist-1");
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            var ourSecondGuid = Guid.NewGuid();
            (await persistRef.Ask<bool>(new SomeEvent { EventName = "rec-test", Guid = ourSecondGuid, Number = 2 }))
                .Should().BeTrue();
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourSecondGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
        }
        
        [Fact]
        public async Task SqlCommon_Snapshot_Can_Recover_L2Db_Snapshot()
        {
            var persistRef = _sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(NewSnapshot, "p-3")), "test-snap-recover-2");
            var ourGuid = Guid.NewGuid();
            
            (await persistRef.Ask<bool>(new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 }))
                .Should().BeTrue();
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            EnsureTerminated(persistRef);
            
            persistRef = _sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(OldSnapshot, "p-3")), "test-snap-recover-2");
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
        }
        
        [Fact]
        public async Task SqlCommon_Snapshot_Can_Persist_L2db_Snapshot()
        {
            var persistRef = _sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(NewSnapshot, "p-4")), "test-snap-persist-2");
            var ourGuid = Guid.NewGuid();
            
            (await persistRef.Ask<bool>(new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 }))
                .Should().BeTrue();
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            EnsureTerminated(persistRef);
            
            persistRef = _sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(OldSnapshot, "p-4")), "test-snap-persist-2");
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(10)))
                .Should().BeTrue();
            
            var ourSecondGuid = Guid.NewGuid();
            (await persistRef.Ask<bool>(new SomeEvent { EventName = "rec-test", Guid = ourSecondGuid, Number = 2 }))
                .Should().BeTrue();
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourSecondGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
        }

        private void EnsureTerminated(IActorRef actorRef)
        {
            _probe.Watch(actorRef);
            actorRef.Tell(PoisonPill.Instance);
            _probe.ExpectTerminated(actorRef);
            _probe.Unwatch(actorRef);
        }
    }
}