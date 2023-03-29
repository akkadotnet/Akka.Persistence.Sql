// -----------------------------------------------------------------------
//  <copyright file="SqlCommonSnapshotCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Internal;
using Akka.Persistence.Sql.Tests.Internal.Events;
using Akka.TestKit;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests
{
    public abstract class SqlCommonSnapshotCompatibilitySpec<T> : IAsyncLifetime where T : ITestContainer
    {
        public SqlCommonSnapshotCompatibilitySpec(T fixture, ITestOutputHelper helper)
        {
            Output = helper;
            Fixture = fixture;
        }

        protected abstract Func<T, Configuration.Config> Config { get; }

        protected T Fixture { get; }
        protected ITestOutputHelper Output { get; }
        protected abstract string OldSnapshot { get; }
        protected abstract string NewSnapshot { get; }
        protected ActorSystem Sys { get; private set; }
        protected Akka.TestKit.Xunit2.TestKit TestKit { get; private set; }
        protected TestProbe Probe { get; private set; }

        public async Task InitializeAsync()
        {
            using var cts = new CancellationTokenSource(10.Seconds());
            try
            {
                await Task.WhenAny(Task.Delay(Timeout.Infinite, cts.Token), Fixture.InitializeDbAsync());
                if (cts.IsCancellationRequested)
                    throw new Exception("Failed to clean up test after 10 seconds");
            }
            finally
            {
                cts.Cancel();
            }
            
            Sys = ActorSystem.Create("test-sys", Config(Fixture));
            TestKit = new Akka.TestKit.Xunit2.TestKit(Sys, Output);
            Probe = TestKit.CreateTestProbe();
        }

        public Task DisposeAsync()
        {
            TestKit.Shutdown();
            return Task.CompletedTask;
        }

        [Fact]
        public void Can_Recover_SqlCommon_Snapshot()
        {
            var persistRef = Sys.ActorOf(Props.Create(() => new SnapshotCompatibilityActor(OldSnapshot, "p-1")));
            var ourGuid = Guid.NewGuid();

            Probe.Send(persistRef, new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 });
            Probe.ExpectMsg(true);
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());

            EnsureTerminated(persistRef);

            persistRef = Sys.ActorOf(Props.Create(() => new SnapshotCompatibilityActor(NewSnapshot, "p-1")));
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
        }

        [Fact]
        public void Can_Persist_SqlCommon_Snapshot()
        {
            var persistRef = Sys.ActorOf(Props.Create(() => new SnapshotCompatibilityActor(OldSnapshot, "p-2")));
            var ourGuid = Guid.NewGuid();

            Probe.Send(persistRef, new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 });
            Probe.ExpectMsg(true);
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());

            EnsureTerminated(persistRef);

            persistRef = Sys.ActorOf(Props.Create(() => new SnapshotCompatibilityActor(NewSnapshot, "p-2")));
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
            var persistRef = Sys.ActorOf(Props.Create(() => new SnapshotCompatibilityActor(NewSnapshot, "p-3")));
            var ourGuid = Guid.NewGuid();

            Probe.Send(persistRef, new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 });
            Probe.ExpectMsg(true);
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());

            EnsureTerminated(persistRef);

            persistRef = Sys.ActorOf(Props.Create(() => new SnapshotCompatibilityActor(OldSnapshot, "p-3")));
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
        }

        [Fact]
        public void SqlCommon_Snapshot_Can_Persist_L2db_Snapshot()
        {
            var persistRef = Sys.ActorOf(Props.Create(() => new SnapshotCompatibilityActor(NewSnapshot, "p-4")));
            var ourGuid = Guid.NewGuid();

            Probe.Send(persistRef, new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 });
            Probe.ExpectMsg(true);
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());

            EnsureTerminated(persistRef);

            persistRef = Sys.ActorOf(Props.Create(() => new SnapshotCompatibilityActor(OldSnapshot, "p-4")));
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 10.Seconds());

            var ourSecondGuid = Guid.NewGuid();
            Probe.Send(persistRef, new SomeEvent { EventName = "rec-test", Guid = ourSecondGuid, Number = 2 });
            Probe.ExpectMsg(true);
            Probe.Send(persistRef, new ContainsEvent { Guid = ourSecondGuid });
            Probe.ExpectMsg(true, 10.Seconds());
        }

        private void EnsureTerminated(IActorRef actorRef)
        {
            Probe.Watch(actorRef);
            actorRef.Tell(PoisonPill.Instance);
            Probe.ExpectTerminated(actorRef);
            Probe.Unwatch(actorRef);
        }
    }
}
