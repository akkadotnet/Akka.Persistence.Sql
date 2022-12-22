using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.TestKit;
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public abstract class SqlCommonJournalCompatibilitySpec: IAsyncLifetime
    {
        protected SqlCommonJournalCompatibilitySpec(ITestOutputHelper outputHelper)
        {
            Output = outputHelper;
        }

        protected ITestOutputHelper Output { get; }

        protected abstract Configuration.Config Config { get; }

        protected abstract string OldJournal { get; }
        protected abstract string NewJournal { get; }
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
        public void Can_Recover_SqlCommon_Journal()
        {
            var persistRef = Sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal, "p-1")), "test-recover-1");
            var ourGuid = Guid.NewGuid();
            var someEvent = new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 };
            
            Probe.Send(persistRef, someEvent);
            Probe.ExpectMsg(someEvent, 5.Seconds());
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
            
            Probe.Watch(persistRef);
            persistRef.Tell(PoisonPill.Instance);
            Probe.ExpectTerminated(persistRef);
            Probe.Unwatch(persistRef);
            
            persistRef = Sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal, "p-1")), "test-recover-1");
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
        }
        
        [Fact]
        public void Can_Persist_SqlCommon_Journal()
        {
            var persistRef = Sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal, "p-2")), "test-persist-1");
            var ourGuid = Guid.NewGuid();
            var someEvent = new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 };
            
            Probe.Send(persistRef, someEvent);
            Probe.ExpectMsg(someEvent, 5.Seconds());
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
            
            Probe.Watch(persistRef);
            persistRef.Tell(PoisonPill.Instance);
            Probe.ExpectTerminated(persistRef);
            Probe.Unwatch(persistRef);
            
            persistRef = Sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal, "p-2")), "test-persist-1");
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
            var persistRef = Sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal, "p-3")), "test-recover-2");
            var ourGuid = Guid.NewGuid();
            var someEvent = new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 };
            
            Probe.Send(persistRef, someEvent);
            Probe.ExpectMsg(someEvent, 5.Seconds());
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
            
            Probe.Watch(persistRef);
            persistRef.Tell(PoisonPill.Instance);
            Probe.ExpectTerminated(persistRef);
            Probe.Unwatch(persistRef);
            
            persistRef = Sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal, "p-3")), "test-recover-2");
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
        }
        
        [Fact]
        public void SqlCommon_Journal_Can_Persist_L2db_Journal()
        {
            var persistRef = Sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal, "p-4")), "test-persist-2");
            var ourGuid = Guid.NewGuid();
            var someEvent = new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 };
            
            Probe.Send(persistRef, someEvent);
            Probe.ExpectMsg(someEvent, 5.Seconds());
            Probe.Send(persistRef, new ContainsEvent { Guid = ourGuid });
            Probe.ExpectMsg(true, 5.Seconds());
            
            Probe.Watch(persistRef);
            persistRef.Tell(PoisonPill.Instance);
            Probe.ExpectTerminated(persistRef);
            Probe.Unwatch(persistRef);
            
            persistRef = Sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal, "p-4")), "test-persist-2");
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
            var persistRef = Sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal, persistenceId)), "test-compat-delete-seqNo");
            
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
            
            Probe.Watch(persistRef);
            persistRef.Tell(PoisonPill.Instance);
            Probe.ExpectTerminated(persistRef);
            Probe.Unwatch(persistRef);
            
            persistRef = Sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal, persistenceId)), "test-compat-delete-seqNo");
            
            Probe.Send(persistRef, new GetSequenceNr());
            var reincaranatedSequenceNrNewJournal = Probe.ExpectMsg<CurrentSequenceNr>(5.Seconds());
            Output.WriteLine($"oldSeq : {currentSequenceNr.SequenceNumber} - newSeq : {reincaranatedSequenceNrNewJournal.SequenceNumber}");
            reincaranatedSequenceNrNewJournal.SequenceNumber.Should().Be(currentSequenceNr.SequenceNumber);
            
            Probe.Watch(persistRef);
            persistRef.Tell(PoisonPill.Instance);
            Probe.ExpectTerminated(persistRef);
            Probe.Unwatch(persistRef);
            
            persistRef = Sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal, persistenceId)), "test-compat-delete-seqNo");
            
            Probe.Send(persistRef, new GetSequenceNr());
            var reincaranatedSequenceNr = Probe.ExpectMsg<CurrentSequenceNr>(5.Seconds());
            Output.WriteLine($"oldSeq : {currentSequenceNr.SequenceNumber} - newSeq : {reincaranatedSequenceNr.SequenceNumber}");
            reincaranatedSequenceNr.SequenceNumber.Should().Be(currentSequenceNr.SequenceNumber);
        }
    }
}