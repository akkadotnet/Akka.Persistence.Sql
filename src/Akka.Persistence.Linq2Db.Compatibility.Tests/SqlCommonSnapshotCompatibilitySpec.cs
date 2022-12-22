using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.TestKit;
using Akka.TestKit.Xunit2.Internals;
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public abstract class SqlCommonSnapshotCompatibilitySpec: IAsyncLifetime
    {
        protected abstract Configuration.Config Config { get; }
        public SqlCommonSnapshotCompatibilitySpec(ITestOutputHelper outputHelper)
        {
            Output = outputHelper;
        }

        protected ITestOutputHelper Output { get; }
        protected abstract string OldSnapshot { get; }
        protected abstract string NewSnapshot { get; }
        protected ActorSystem Sys { get; private set;  }
        protected Akka.TestKit.Xunit2.TestKit TestKit { get; private set; }
        protected TestProbe Probe { get; private set; }
        
        public Task InitializeAsync()
        {
            Sys = ActorSystem.Create("test-sys", Config);
            TestKit = new Akka.TestKit.Xunit2.TestKit(Sys, Output);
            Probe = TestKit.CreateTestProbe();
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await Sys.Terminate();
        }
        
        [Fact]
        public void Can_Recover_SqlCommon_Snapshot()
        {
            var persistRef = Sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(OldSnapshot, "p-1")), "test-snap-recover-1");
            var ourGuid = Guid.NewGuid();

            Probe.Send(persistRef, new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 });
            Probe.ExpectMsg(true);
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());

            Probe.Watch(persistRef);
            persistRef.Tell(PoisonPill.Instance);
            Probe.ExpectTerminated(persistRef);
            Probe.Unwatch(persistRef);
            
            persistRef = Sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(NewSnapshot, "p-1")), "test-snap-recover-1");
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
        }
        
        [Fact]
        public void Can_Persist_SqlCommon_Snapshot()
        {
            var persistRef = Sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(OldSnapshot, "p-2")), "test-snap-persist-1");
            var ourGuid = Guid.NewGuid();

            Probe.Send(persistRef, new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 });
            Probe.ExpectMsg(true);
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
            
            Probe.Watch(persistRef);
            persistRef.Tell(PoisonPill.Instance);
            Probe.ExpectTerminated(persistRef);
            Probe.Unwatch(persistRef);
            
            persistRef = Sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(NewSnapshot, "p-2")), "test-snap-persist-1");
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
            
            var ourSecondGuid = Guid.NewGuid();
            Probe.Send(persistRef, new SomeEvent { EventName = "rec-test", Guid = ourSecondGuid, Number = 2 });
            Probe.ExpectMsg(true);
            Probe.Send(persistRef, new ContainsEvent { Guid = ourSecondGuid });
            Probe.ExpectMsg(true, 5.Seconds());
        }
        
        [Fact]
        public void SqlCommon_Snapshot_Can_Recover_L2Db_Snapshot()
        {
            var persistRef = Sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(NewSnapshot, "p-3")), "test-snap-recover-2");
            var ourGuid = Guid.NewGuid();
            
            Probe.Send(persistRef, new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 });
            Probe.ExpectMsg(true);
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
            
            Probe.Watch(persistRef);
            persistRef.Tell(PoisonPill.Instance);
            Probe.ExpectTerminated(persistRef);
            Probe.Unwatch(persistRef);
            
            persistRef = Sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(OldSnapshot, "p-3")), "test-snap-recover-2");
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
        }
        
        [Fact]
        public void SqlCommon_Snapshot_Can_Persist_L2db_Snapshot()
        {
            var persistRef = Sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(NewSnapshot, "p-4")), "test-snap-persist-2");
            var ourGuid = Guid.NewGuid();
            
            Probe.Send(persistRef, new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 });
            Probe.ExpectMsg(true);
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
            
            Probe.Watch(persistRef);
            persistRef.Tell(PoisonPill.Instance);
            Probe.ExpectTerminated(persistRef);
            Probe.Unwatch(persistRef);
            
            persistRef = Sys.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(OldSnapshot, "p-4")), "test-snap-persist-2");
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 10.Seconds());
            
            var ourSecondGuid = Guid.NewGuid();
            Probe.Send(persistRef, new SomeEvent { EventName = "rec-test", Guid = ourSecondGuid, Number = 2 });
            Probe.ExpectMsg(true);
            Probe.Send(persistRef, new ContainsEvent { Guid = ourSecondGuid });
            Probe.ExpectMsg(true, 10.Seconds());
        }
    }
}