using System;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.TestKit.Xunit2.Internals;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;
using Xunit;
using Xunit.Abstractions;
using Config = Docker.DotNet.Models.Config;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public abstract class SqlCommonJournalCompatibilitySpec
    {
        

        public SqlCommonJournalCompatibilitySpec(ITestOutputHelper outputHelper)
        {
            Output = outputHelper;
        }

        public ITestOutputHelper Output { get; set; }

        protected void InitializeLogger(ActorSystem system)
        {
            if (Output != null)
            {
                var extSystem = (ExtendedActorSystem)system;
                var logger = extSystem.SystemActorOf(Props.Create(() => new TestOutputLogger(Output)), "log-test");
                logger.Tell(new InitializeLogger(system.EventStream));
            }
        }

        protected abstract Configuration.Config Config { get; }

        protected abstract string OldJournal { get; }
        protected abstract string NewJournal { get; }
        [Fact]
        public async Task Can_Recover_SqlCommon_Journal()
        {
            var sys1 = ActorSystem.Create("first",
                Config);
            InitializeLogger(sys1);
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal,
                    "p-1")), "test-recover-1");
            var ourGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid, Number = 1});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid}, TimeSpan.FromSeconds(5)).Result);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef =  sys1.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal,
                    "p-1")), "test-recover-1");
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid},TimeSpan.FromSeconds(5)).Result);
        }
        [Fact]
        public async Task Can_Persist_SqlCommon_Journal()
        {
            var sys1 = ActorSystem.Create("first",
                Config);
            InitializeLogger(sys1);
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal,
                    "p-2")), "test-persist-1");
            var ourGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid, Number = 1});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid}, TimeSpan.FromSeconds(5)).Result);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef =  sys1.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal,
                    "p-2")), "test-persist-1");
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid},TimeSpan.FromSeconds(10)).Result);
            var ourSecondGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourSecondGuid, Number = 2});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourSecondGuid},TimeSpan.FromSeconds(5)).Result);
        }
        
        [Fact]
        public async Task SqlCommon_Journal_Can_Recover_L2Db_Journal()
        {
            var sys1 = ActorSystem.Create("first",
                Config);
            InitializeLogger(sys1);
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal,
                    "p-3")), "test-recover-2");
            var ourGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid, Number = 1});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid}, TimeSpan.FromSeconds(5)).Result);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef = sys1.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal,
                    "p-3")), "test-recover-2");
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid},TimeSpan.FromSeconds(5)).Result);
        }
        [Fact]
        public async Task SqlCommon_Journal_Can_Persist_L2db_Journal()
        {
            var sys1 = ActorSystem.Create("first",
                Config);
            InitializeLogger(sys1);
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal,
                    "p-4")), "test-persist-2");
            var ourGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid, Number = 1});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid}, TimeSpan.FromSeconds(5)).Result);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef =  sys1.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal,
                    "p-4")), "test-persist-2");
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid},TimeSpan.FromSeconds(10)).Result);
            var ourSecondGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourSecondGuid, Number = 2});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourSecondGuid},TimeSpan.FromSeconds(5)).Result);
        }
        
        [Fact]
        public async Task L2db_Journal_Delete_Compat_mode_Preserves_proper_SequenceNr()
        {
            var sys1 = ActorSystem.Create("first",
                Config);
            InitializeLogger(sys1);
            var persistenceId = "d-1";
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal,
                    persistenceId)), "test-compat-delete-seqno");
            var ourGuid1 = Guid.NewGuid();
            var ourGuid2 = Guid.NewGuid();
            var ourGuid3 = Guid.NewGuid();
            var ourGuid4 = Guid.NewGuid();
            var ourGuid5 = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid1, Number = 1});
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid2, Number = 2});
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid3, Number = 3});
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid4, Number = 4});
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid5, Number = 5});
            Assert.True((await persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid5}, TimeSpan.FromSeconds(5))));
            var currentSequenceNr = await persistRef.Ask<CurrentSequenceNr>(new GetSequenceNr(), TimeSpan.FromSeconds(5));
            var delResult =
                await persistRef.Ask<object>(
                    new DeleteUpToSequenceNumber(currentSequenceNr.SequenceNumber));
            Assert.True(delResult is DeleteMessagesSuccess);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef = sys1.ActorOf(Props.Create(() =>
                new JournalCompatActor(NewJournal,
                    persistenceId)), "test-compat-delete-seqno");
            var reIncaranatedSequenceNrNewJournal = await persistRef.Ask<CurrentSequenceNr>(new GetSequenceNr(),TimeSpan.FromSeconds(5));
            Output.WriteLine($"oldSeq : {currentSequenceNr.SequenceNumber} - newSeq : {reIncaranatedSequenceNrNewJournal.SequenceNumber}");
            Assert.Equal(currentSequenceNr.SequenceNumber,reIncaranatedSequenceNrNewJournal.SequenceNumber);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef = sys1.ActorOf(Props.Create(() =>
                new JournalCompatActor(OldJournal,
                    persistenceId)), "test-compat-delete-seqno");
            var reIncaranatedSequenceNr = await persistRef.Ask<CurrentSequenceNr>(new GetSequenceNr(),TimeSpan.FromSeconds(5));
            Output.WriteLine($"oldSeq : {currentSequenceNr.SequenceNumber} - newSeq : {reIncaranatedSequenceNr.SequenceNumber}");
            Assert.Equal(currentSequenceNr.SequenceNumber,reIncaranatedSequenceNr.SequenceNumber);
        }
    }
}