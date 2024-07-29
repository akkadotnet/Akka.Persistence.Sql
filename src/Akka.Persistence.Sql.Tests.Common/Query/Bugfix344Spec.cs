// -----------------------------------------------------------------------
//  <copyright file="Bugfix344Spec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Streams;
using Akka.Streams.TestKit;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Akka.Persistence.Sql.Tests.Common.Query
{
    public abstract class Bugfix344Spec<T> : Akka.TestKit.Xunit2.TestKit, IAsyncLifetime where T : ITestContainer
    {
        protected Bugfix344Spec(ITestOutputHelper output, T fixture) : base(config:Config(TagMode.TagTable, fixture), output:output)
        {
            ReadJournal = Sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
            Materializer = Sys.Materializer();
        } 
        
        protected ActorMaterializer Materializer { get; }
        protected IReadJournal? ReadJournal { get; set; }

        public Task InitializeAsync()
            => Task.CompletedTask;

        public Task DisposeAsync()
            => Task.CompletedTask;

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

        private static Configuration.Config Config(TagMode tagMode, T fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            return ConfigurationFactory.ParseString(
                    $$"""

                      akka.loglevel = INFO
                      akka.persistence.journal.plugin = "akka.persistence.journal.sql"
                      akka.persistence.journal.sql {
                          event-adapters {
                              color-tagger  = "Akka.Persistence.TCK.Query.ColorFruitTagger, Akka.Persistence.TCK"
                          }
                          event-adapter-bindings = {
                              "System.String" = color-tagger
                          }
                          provider-name = "{{fixture.ProviderName}}"
                          tag-write-mode = "{{tagMode}}"
                          connection-string = "{{fixture.ConnectionString}}"
                      }
                      akka.persistence.query.journal.sql {
                          provider-name = "{{fixture.ProviderName}}"
                          connection-string = "{{fixture.ConnectionString}}"
                          tag-read-mode = "{{tagMode}}"
                          refresh-interval = 1s
                      }
                      akka.test.single-expect-default = 10s
                      """)
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }
    }
}
