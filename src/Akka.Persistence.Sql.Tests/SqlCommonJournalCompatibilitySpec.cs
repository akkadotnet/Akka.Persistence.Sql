// -----------------------------------------------------------------------
//  <copyright file="SqlCommonJournalCompatibilitySpec.cs" company="Akka.NET Project">
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
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests
{
    public abstract class SqlCommonJournalCompatibilitySpec<T> : IAsyncLifetime where T : ITestContainer
    {
        protected SqlCommonJournalCompatibilitySpec(T fixture, ITestOutputHelper outputHelper)
        {
            Fixture = fixture;
            Output = outputHelper;
        }

        protected T Fixture { get; }
        protected ITestOutputHelper Output { get; }

        protected abstract Func<T, Configuration.Config> Config { get; }

        protected abstract string OldJournal { get; }
        protected abstract string NewJournal { get; }
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
        public void Can_Recover_SqlCommon_Journal()
        {
            var persistRef = Sys.ActorOf(Props.Create(() => new JournalCompatibilityActor(OldJournal, "p-1")));
            var ourGuid = Guid.NewGuid();
            var someEvent = new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 };

            Probe.Send(persistRef, someEvent);
            Probe.ExpectMsg(someEvent, 5.Seconds());
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());

            EnsureTerminated(persistRef);

            persistRef = Sys.ActorOf(Props.Create(() => new JournalCompatibilityActor(NewJournal, "p-1")));
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
        }

        [Fact]
        public void Can_Persist_SqlCommon_Journal()
        {
            var persistRef = Sys.ActorOf(Props.Create(() => new JournalCompatibilityActor(OldJournal, "p-2")));
            var ourGuid = Guid.NewGuid();
            var someEvent = new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 };

            Probe.Send(persistRef, someEvent);
            Probe.ExpectMsg(someEvent, 5.Seconds());
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());

            EnsureTerminated(persistRef);

            persistRef = Sys.ActorOf(Props.Create(() => new JournalCompatibilityActor(NewJournal, "p-2")));
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());

            var ourSecondGuid = Guid.NewGuid();
            var secondEvent = new SomeEvent { EventName = "rec-test", Guid = ourSecondGuid, Number = 2 };

            Probe.Send(persistRef, secondEvent);
            Probe.ExpectMsg(secondEvent, 5.Seconds());
            Probe.Send(persistRef, new ContainsEvent { Guid = ourSecondGuid });
            Probe.ExpectMsg(true, 5.Seconds());
        }

        [Fact]
        public void SqlCommon_Journal_Can_Recover_L2Db_Journal()
        {
            var persistRef = Sys.ActorOf(Props.Create(() => new JournalCompatibilityActor(NewJournal, "p-3")));
            var ourGuid = Guid.NewGuid();
            var someEvent = new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 };

            Probe.Send(persistRef, someEvent);
            Probe.ExpectMsg(someEvent, 5.Seconds());
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());

            EnsureTerminated(persistRef);

            persistRef = Sys.ActorOf(Props.Create(() => new JournalCompatibilityActor(OldJournal, "p-3")));
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
        }

        [Fact]
        public void SqlCommon_Journal_Can_Persist_L2db_Journal()
        {
            var persistRef = Sys.ActorOf(Props.Create(() => new JournalCompatibilityActor(NewJournal, "p-4")));
            var ourGuid = Guid.NewGuid();
            var someEvent = new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 };

            Probe.Send(persistRef, someEvent);
            Probe.ExpectMsg(someEvent, 5.Seconds());
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());

            EnsureTerminated(persistRef);

            persistRef = Sys.ActorOf(Props.Create(() => new JournalCompatibilityActor(OldJournal, "p-4")));
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());

            var ourSecondGuid = Guid.NewGuid();
            var secondEvent = new SomeEvent { EventName = "rec-test", Guid = ourSecondGuid, Number = 2 };
            Probe.Send(persistRef, secondEvent);
            Probe.ExpectMsg(secondEvent, 5.Seconds());
            Probe.Send(persistRef, new ContainsEvent { Guid = ourSecondGuid });
            Probe.ExpectMsg(true, 5.Seconds());
        }

        [Fact]
        public void L2db_Journal_Delete_Compat_mode_Preserves_proper_SequenceNr()
        {
            const string persistenceId = "d-1";
            var persistRef = Sys.ActorOf(Props.Create(() => new JournalCompatibilityActor(NewJournal, persistenceId)));

            var ourGuid1 = Guid.NewGuid();
            var ourGuid2 = Guid.NewGuid();
            var ourGuid3 = Guid.NewGuid();
            var ourGuid4 = Guid.NewGuid();
            var ourGuid5 = Guid.NewGuid();
            var event1 = new SomeEvent { EventName = "rec-test", Guid = ourGuid1, Number = 1 };
            var event2 = new SomeEvent { EventName = "rec-test", Guid = ourGuid2, Number = 2 };
            var event3 = new SomeEvent { EventName = "rec-test", Guid = ourGuid3, Number = 3 };
            var event4 = new SomeEvent { EventName = "rec-test", Guid = ourGuid4, Number = 4 };
            var event5 = new SomeEvent { EventName = "rec-test", Guid = ourGuid5, Number = 5 };

            Probe.Send(persistRef, event1);
            Probe.ExpectMsg(event1, 5.Seconds());
            Probe.Send(persistRef, event2);
            Probe.ExpectMsg(event2, 5.Seconds());
            Probe.Send(persistRef, event3);
            Probe.ExpectMsg(event3, 5.Seconds());
            Probe.Send(persistRef, event4);
            Probe.ExpectMsg(event4, 5.Seconds());
            Probe.Send(persistRef, event5);
            Probe.ExpectMsg(event5, 5.Seconds());

            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid5 });
            Probe.ExpectMsg(true, 5.Seconds());

            Probe.Send(persistRef, new GetSequenceNr());
            var currentSequenceNr = Probe.ExpectMsg<CurrentSequenceNr>(5.Seconds());

            Probe.Send(persistRef, new DeleteUpToSequenceNumber(currentSequenceNr.SequenceNumber));
            var delResult = Probe.ExpectMsg<object>(5.Seconds());
            delResult.Should().BeOfType<DeleteMessagesSuccess>();

            EnsureTerminated(persistRef);

            persistRef = Sys.ActorOf(Props.Create(() => new JournalCompatibilityActor(NewJournal, persistenceId)));

            Probe.Send(persistRef, new GetSequenceNr());
            var reincarnatedSequenceNrNewJournal = Probe.ExpectMsg<CurrentSequenceNr>(5.Seconds());
            Output.WriteLine($"oldSeq: {currentSequenceNr.SequenceNumber} - newSeq: {reincarnatedSequenceNrNewJournal.SequenceNumber}");
            reincarnatedSequenceNrNewJournal.SequenceNumber.Should().Be(currentSequenceNr.SequenceNumber);

            EnsureTerminated(persistRef);

            persistRef = Sys.ActorOf(Props.Create(() => new JournalCompatibilityActor(OldJournal, persistenceId)));

            Probe.Send(persistRef, new GetSequenceNr());
            var reincarnatedSequenceNr = Probe.ExpectMsg<CurrentSequenceNr>(5.Seconds());
            Output.WriteLine($"oldSeq: {currentSequenceNr.SequenceNumber} - newSeq: {reincarnatedSequenceNr.SequenceNumber}");
            reincarnatedSequenceNr.SequenceNumber.Should().Be(currentSequenceNr.SequenceNumber);
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
