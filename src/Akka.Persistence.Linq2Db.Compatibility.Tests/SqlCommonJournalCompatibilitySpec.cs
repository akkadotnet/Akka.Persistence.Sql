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
    public abstract class SqlCommonJournalCompatibilitySpec: IAsyncLifetime
    {
        private Akka.TestKit.Xunit2.TestKit _testKit;
        private ActorSystem _sys;
        private TestProbe _probe;
        private ILoggingAdapter _log;
        private readonly ITestOutputHelper _helper;
        
        public SqlCommonJournalCompatibilitySpec(ITestOutputHelper outputHelper)
        {
            _helper = outputHelper;
        }

        protected abstract Configuration.Config Config { get; }

        protected abstract string OldJournal { get; }
        protected abstract string NewJournal { get; }
        
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
        public async Task Can_Recover_SqlCommon_Journal()
        {
            var persistRef = _sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal, "p-1")), "test-recover-1");
            var ourGuid = Guid.NewGuid();
            var someEvent = new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 };
            (await persistRef.Ask<SomeEvent>(someEvent, 5.Seconds())).Should().Be(someEvent);
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();

            EnsureTerminated(persistRef);
            
            persistRef = _sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal, "p-1")), "test-recover-1");
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
        }
        
        [Fact]
        public async Task Can_Persist_SqlCommon_Journal()
        {
            var persistRef = _sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal, "p-2")), "test-persist-1");
            var ourGuid = Guid.NewGuid();
            var someEvent = new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 };
            (await persistRef.Ask<SomeEvent>(someEvent, 5.Seconds())).Should().Be(someEvent);
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            EnsureTerminated(persistRef);
            
            persistRef = _sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal, "p-2")), "test-persist-1");
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            var ourSecondGuid = Guid.NewGuid();
            var secondEvent = new SomeEvent { EventName = "rec-test", Guid = ourSecondGuid, Number = 2 };
            (await persistRef.Ask<SomeEvent>(secondEvent, 5.Seconds())).Should().Be(secondEvent);
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourSecondGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
        }
        
        [Fact]
        public async Task SqlCommon_Journal_Can_Recover_L2Db_Journal()
        {
            var persistRef = _sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal, "p-3")), "test-recover-2");
            var ourGuid = Guid.NewGuid();
            var someEvent = new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 };
            (await persistRef.Ask<SomeEvent>(someEvent, 5.Seconds())).Should().Be(someEvent);
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            EnsureTerminated(persistRef);
            
            persistRef = _sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal, "p-3")), "test-recover-2");
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
        }
        [Fact]
        public async Task SqlCommon_Journal_Can_Persist_L2db_Journal()
        {
            var persistRef = _sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal, "p-4")), "test-persist-2");
            var ourGuid = Guid.NewGuid();
            var someEvent = new SomeEvent { EventName = "rec-test", Guid = ourGuid, Number = 1 };
            (await persistRef.Ask<SomeEvent>(someEvent, 5.Seconds())).Should().Be(someEvent);
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            EnsureTerminated(persistRef);
            
            persistRef = _sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal, "p-4")), "test-persist-2");
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            var ourSecondGuid = Guid.NewGuid();
            var secondEvent = new SomeEvent { EventName = "rec-test", Guid = ourSecondGuid, Number = 2 };
            (await persistRef.Ask<SomeEvent>(secondEvent, 5.Seconds())).Should().Be(secondEvent);
            (await persistRef.Ask<bool>(new ContainsEvent { Guid = ourSecondGuid }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
        }
        
        [Fact]
        public async Task L2db_Journal_Delete_Compat_mode_Preserves_proper_SequenceNr()
        {
            const string persistenceId = "d-1";
            var persistRef = _sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal, persistenceId)), "test-compat-delete-seqno");
            
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

            (await persistRef.Ask<SomeEvent>(event1, 5.Seconds())).Should().Be(event1);
            (await persistRef.Ask<SomeEvent>(event2, 5.Seconds())).Should().Be(event2);
            (await persistRef.Ask<SomeEvent>(event3, 5.Seconds())).Should().Be(event3);
            (await persistRef.Ask<SomeEvent>(event4, 5.Seconds())).Should().Be(event4);
            (await persistRef.Ask<SomeEvent>(event5, 5.Seconds())).Should().Be(event5);
            (await persistRef.Ask<bool>(new ContainsEvent() { Guid = ourGuid5 }, TimeSpan.FromSeconds(5)))
                .Should().BeTrue();
            
            var currentSequenceNr = await persistRef.Ask<CurrentSequenceNr>(new GetSequenceNr(), TimeSpan.FromSeconds(5));
            var delResult = await persistRef.Ask<object>(new DeleteUpToSequenceNumber(currentSequenceNr.SequenceNumber));
            delResult.Should().BeOfType<DeleteMessagesSuccess>();
            
            EnsureTerminated(persistRef);
            
            persistRef = _sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal, persistenceId)), "test-compat-delete-seqno");
            var reincaranatedSequenceNrNewJournal = await persistRef.Ask<CurrentSequenceNr>(new GetSequenceNr(),TimeSpan.FromSeconds(5));
            _log.Info($"oldSeq : {currentSequenceNr.SequenceNumber} - newSeq : {reincaranatedSequenceNrNewJournal.SequenceNumber}");
            reincaranatedSequenceNrNewJournal.SequenceNumber.Should().Be(currentSequenceNr.SequenceNumber);
            
            EnsureTerminated(persistRef);
            
            persistRef = _sys.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal, persistenceId)), "test-compat-delete-seqno");
            var reincaranatedSequenceNr = await persistRef.Ask<CurrentSequenceNr>(new GetSequenceNr(),TimeSpan.FromSeconds(5));
            _log.Info($"oldSeq : {currentSequenceNr.SequenceNumber} - newSeq : {reincaranatedSequenceNr.SequenceNumber}");
            reincaranatedSequenceNr.SequenceNumber.Should().Be(currentSequenceNr.SequenceNumber);
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