using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.TestKit.Xunit2.Internals;
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public abstract class SqlCommonSnapshotCompatibilitySpec
    {
        protected abstract Configuration.Config Config { get; }
        public SqlCommonSnapshotCompatibilitySpec(ITestOutputHelper outputHelper)
        {
            Output = outputHelper;
        }

        protected void InitializeLogger(ActorSystem system)
        {
            if (Output != null)
            {
                var extSystem = (ExtendedActorSystem)system;
                var logger = extSystem.SystemActorOf(Props.Create(() => new TestOutputLogger(Output)), "log-test");
                logger.Tell(new InitializeLogger(system.EventStream));
            }
        }
        
        public ITestOutputHelper Output { get; }
        protected abstract string OldSnapshot { get; }
        protected abstract string NewSnapshot { get; }
        
        [Fact]
        public async Task Can_Recover_SqlCommon_Snapshot()
        {
            var sys1 = ActorSystem.Create("first", Config);
            InitializeLogger(sys1);
            
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(OldSnapshot, "p-1")), "test-snap-recover-1");
            var ourGuid = Guid.NewGuid();
            
            (await persistRef.Ask<bool>(new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 }))
                .Should().BeTrue();
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            (await persistRef.GracefulStop(10.Seconds())).Should().BeTrue();
            // Intentionally being called twice to make sure that the actor state inside the user guardian has been cleared 
            (await persistRef.GracefulStop(10.Seconds())).Should().BeTrue();
            
            persistRef =  sys1.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(NewSnapshot, "p-1")), "test-snap-recover-1");
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
        }
        [Fact]
        public async Task Can_Persist_SqlCommon_Snapshot()
        {
            var sys1 = ActorSystem.Create("first", Config);
            InitializeLogger(sys1);
            
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(OldSnapshot, "p-2")), "test-snap-persist-1");
            var ourGuid = Guid.NewGuid();
            
            (await persistRef.Ask<bool>(new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 }))
                .Should().BeTrue();
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            (await persistRef.GracefulStop(10.Seconds())).Should().BeTrue();
            // Intentionally being called twice to make sure that the actor state inside the user guardian has been cleared 
            (await persistRef.GracefulStop(10.Seconds())).Should().BeTrue();
            
            persistRef = sys1.ActorOf(Props.Create(() =>
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
            var sys1 = ActorSystem.Create("first", Config);
            InitializeLogger(sys1);
            
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(NewSnapshot, "p-3")), "test-snap-recover-2");
            var ourGuid = Guid.NewGuid();
            
            (await persistRef.Ask<bool>(new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 }))
                .Should().BeTrue();
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            (await persistRef.GracefulStop(10.Seconds())).Should().BeTrue();
            // Intentionally being called twice to make sure that the actor state inside the user guardian has been cleared 
            (await persistRef.GracefulStop(10.Seconds())).Should().BeTrue();
            
            persistRef = sys1.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(OldSnapshot, "p-3")), "test-snap-recover-2");
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
        }
        
        [Fact]
        public async Task SqlCommon_Snapshot_Can_Persist_L2db_Snapshot()
        {
            var sys1 = ActorSystem.Create("first", Config);
            InitializeLogger(sys1);
            
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(NewSnapshot, "p-4")), "test-snap-persist-2");
            var ourGuid = Guid.NewGuid();
            
            (await persistRef.Ask<bool>(new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 }))
                .Should().BeTrue();
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            (await persistRef.GracefulStop(10.Seconds())).Should().BeTrue();
            // Intentionally being called twice to make sure that the actor state inside the user guardian has been cleared 
            (await persistRef.GracefulStop(10.Seconds())).Should().BeTrue();
            
            persistRef = sys1.ActorOf(Props.Create(() =>
                new SnapshotCompatActor(OldSnapshot, "p-4")), "test-snap-persist-2");
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(10)))
                .Should().BeTrue();
            
            var ourSecondGuid = Guid.NewGuid();
            (await persistRef.Ask<bool>(new SomeEvent { EventName = "rec-test", Guid = ourSecondGuid, Number = 2 }))
                .Should().BeTrue();
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourSecondGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
        }
    }
}