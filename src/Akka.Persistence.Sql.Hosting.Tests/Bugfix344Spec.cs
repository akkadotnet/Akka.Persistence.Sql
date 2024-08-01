// -----------------------------------------------------------------------
//  <copyright file="Bugfix344Spec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Hosting;
using Akka.Persistence.Hosting;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Query;
using Akka.Streams;
using Akka.Streams.TestKit;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
using Akka.Persistence.Sql.Query;
using FluentAssertions.Extensions;

namespace Akka.Persistence.Sql.Hosting.Tests
{
    public class Bugfix344Spec : Akka.Hosting.TestKit.TestKit, IClassFixture<SqliteContainer>
    {
        private ActorMaterializer? Materializer;
        private IReadJournal? ReadJournal;
        private readonly SqliteContainer _fixture;

        public Bugfix344Spec(ITestOutputHelper output, SqliteContainer fixture) : base(nameof(Bugfix344Spec), output)
        {
            _fixture = fixture;
            
            if (!_fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
        }

        protected override async Task BeforeTestStart()
        {
            await base.BeforeTestStart();
            ReadJournal = Sys.ReadJournalFor<SqlReadJournal>("akka.persistence.query.journal.custom");
            Materializer = Sys.Materializer();
        }

        protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            var journalOptions = new SqlJournalOptions(true, "custom")
            {
                ProviderName = _fixture.ProviderName,
                ConnectionString = _fixture.ConnectionString,
                TagStorageMode = TagMode.TagTable,
                Adapters = new AkkaPersistenceJournalBuilder("custom", builder),
                QueryRefreshInterval = 1.Seconds(),
                AutoInitialize = true,
            };
            journalOptions.Adapters.AddWriteEventAdapter<ColorFruitTagger>("color-tagger", [typeof(string)]);

            var snapshotOptions = new SqlSnapshotOptions(true, "custom")
            {
                ProviderName = _fixture.ProviderName,
                ConnectionString = _fixture.ConnectionString, 
                AutoInitialize = true,
            };

            builder.WithSqlPersistence(journalOptions, snapshotOptions);
        }
        
        [Fact]
        public async Task ReadJournal_should_initialize_tables_when_started_before_WriteJournal()
        {
            if (ReadJournal is not ICurrentEventsByTagQuery queries)
                throw IsTypeException.ForMismatchedType(nameof(IEventsByTagQuery), ReadJournal?.GetType().Name ?? "null");

            // should just not return 
            await EventFilter.Error().ExpectAsync(
                0,
                async () =>
                {
                    var blackSrc = queries.CurrentEventsByTag("random-unused-tag", offset: NoOffset.Instance);
                    var probe = blackSrc.RunWith(this.SinkProbe<EventEnvelope>(), Materializer);
                    probe.Request(2);
                    
                    // query should just gracefully exit
                    await probe.ExpectCompleteAsync();
                });
        }
        
    }
}
