// -----------------------------------------------------------------------
//  <copyright file="BaselineJournalBuilderSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Event;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Query;
using Akka.Streams;
using Akka.Streams.TestKit;
using FluentAssertions;
using FluentAssertions.Extensions;
using LinqToDB;
using Xunit;

namespace Akka.Persistence.Sql.Hosting.Tests
{
    /// <summary>
    /// Baseline test to validate current journalBuilder functionality before refactoring
    /// </summary>
    public class BaselineJournalBuilderSpec : Akka.Hosting.TestKit.TestKit, IClassFixture<SqliteContainer>
    {
        private const string PId = "baseline-test";
        private readonly SqliteContainer _fixture;

        public BaselineJournalBuilderSpec(ITestOutputHelper output, SqliteContainer fixture)
            : base(nameof(BaselineJournalBuilderSpec), output)
        {
            _fixture = fixture;

            if (!_fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
        }

        protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            // Test the refactored pattern to ensure basic persistence works
            builder.WithSqlPersistence(
                connectionString: _fixture.ConnectionString,
                providerName: _fixture.ProviderName);

            builder.StartActors((system, registry) =>
            {
                var actor = system.ActorOf(Props.Create(() => new TestPersistentActor(PId)));
                registry.Register<TestPersistentActor>(actor);
            });
        }

        [Fact]
        public async Task Refactored_hosting_should_support_basic_persistence()
        {
            // Arrange
            var actor = ActorRegistry.Get<TestPersistentActor>();

            // Act - persist an event
            actor.Tell("test-event");
            await ExpectMsgAsync<string>("ACK", 3.Seconds());

            // Verify the event was persisted
            var readJournal = Sys.ReadJournalFor<SqlReadJournal>("akka.persistence.query.journal.sql");
            var source = readJournal.CurrentEventsByPersistenceId(PId, 0, long.MaxValue);
            var probe = source.RunWith(this.SinkProbe<EventEnvelope>(), Sys.Materializer());

            probe.Request(1);
            var envelope = await probe.ExpectNextAsync(3.Seconds());
            envelope.PersistenceId.Should().Be(PId);
            envelope.Event.Should().Be("test-event");
            await probe.ExpectCompleteAsync();
        }

        private class TestPersistentActor : ReceivePersistentActor
        {
            public TestPersistentActor(string persistenceId)
            {
                PersistenceId = persistenceId;

                Command<string>(str =>
                {
                    var sender = Sender;
                    Persist(str, _ => sender.Tell("ACK"));
                });
            }

            public override string PersistenceId { get; }
        }
    }
}
