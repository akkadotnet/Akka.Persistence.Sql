// -----------------------------------------------------------------------
//  <copyright file="Issue502SpecsBase.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Journal;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Dao;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Query.Dao;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK;
using Akka.Streams;
using Akka.Streams.TestKit;
using Akka.TestKit;
using Akka.TestKit.Extensions;
using Akka.Util;
using FluentAssertions;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query
{
    public abstract class Issue502SpecsBase<T> : PluginSpec where T : ITestContainer
    {
        private readonly TestProbe _senderProbe;
        private readonly ActorMaterializer _materializer;
        
        protected Issue502SpecsBase(TagMode tagMode, ITestOutputHelper output, string name, T fixture)
            : base(FromConfig(Config(tagMode, fixture)), name, output)
        {
            // Force start read journal
            _ = Journal;

            _senderProbe = CreateTestProbe();
            _materializer = Sys.Materializer();
        }

        protected IActorRef Journal => Extension.JournalFor(null);
        
        protected SqlReadJournal ReadJournal => PersistenceQuery.Get(Sys).ReadJournalFor<SqlReadJournal>("akka.persistence.query.journal.sql");

        protected override bool SupportsSerialization => true;
        
        private static Configuration.Config Config(TagMode tagMode, T fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            return ConfigurationFactory.ParseString(
                    $$"""
                      akka {
                          loglevel = INFO
                          persistence {
                              journal {
                                  plugin = "akka.persistence.journal.sql"
                                  auto-start-journals = [ "akka.persistence.journal.sql" ]
                                  sql {
                                      event-adapters {
                                          color-tagger  = "Akka.Persistence.Sql.Tests.ColorFruitTagger, Akka.Persistence.Sql.Tests"
                                      }
                                      event-adapter-bindings = {
                                          "System.String" = color-tagger
                                      }
                                      provider-name = "{{fixture.ProviderName}}"
                                      tag-write-mode = "{{tagMode}}"
                                      connection-string = "{{fixture.ConnectionString}}"
                                  }
                              }
                              query.journal.sql {
                                  provider-name = "{{fixture.ProviderName}}"
                                  connection-string = "{{fixture.ConnectionString}}"
                                  tag-read-mode = "{{tagMode}}"
                                  refresh-interval = 1s
                                  
                                  # what is referred as "batchSize" in code
                                  max-buffer-size = 3
                              }
                          }
                      }
                      akka.test.single-expect-default = 10s
                      """)
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }

        [Fact (DisplayName = "A full query batch with one element adapted to EventSequence.Empty should still run to the end")]
        public async Task MissingSequenceTest()
        {
            var a = Sys.ActorOf(Query.TestActor.Props("a"));
            var b = Sys.ActorOf(Query.TestActor.Props("b"));
            
            a.Tell("hello");
            await ExpectMsgAsync("hello-done");
            b.Tell("a black car");
            await ExpectMsgAsync("a black car-done");
            a.Tell("something else");
            await ExpectMsgAsync("something else-done");
            a.Tell("a green banana");
            await ExpectMsgAsync("a green banana-done");
            a.Tell("an invalid apple"); // will be missing on query
            await ExpectMsgAsync("an invalid apple-done");
            b.Tell("a green leaf");
            await ExpectMsgAsync("a green leaf-done");
            b.Tell("a repeated green leaf");
            await ExpectMsgAsync("a repeated green leaf-done");
            b.Tell("a repeated green leaf");
            await ExpectMsgAsync("a repeated green leaf-done");
            
            var reader = ReadJournal;
            var probe = reader.CurrentAllEvents(Offset.NoOffset())
                .RunWith(this.SinkProbe<EventEnvelope>(), _materializer);

            await probe.ExpectSubscriptionAsync().ShouldCompleteWithin(1.Seconds());
            await probe.RequestAsync(10);
        
            await ValidateRepresentation(probe, "a", 1L, "hello");
            await ValidateRepresentation(probe, "b", 1L, "a black car");
            await ValidateRepresentation(probe, "a", 2L, "something else");
            await ValidateRepresentation(probe, "a", 3L, "a green banana");
            await ValidateRepresentation(probe, "b", 2L, "a green leaf");
            await ValidateRepresentation(probe, "b", 3L, "a repeated green leaf");
            await ValidateRepresentation(probe, "b", 4L, "a repeated green leaf");
            await probe.ExpectCompleteAsync();
        }
        
        [Fact (DisplayName = "A full query batch with one element adapted to EventSequence with multiple entries should still run to the end")]
        public async Task DuplicatedSequenceTest()
        {
            var a = Sys.ActorOf(Query.TestActor.Props("a"));
            var b = Sys.ActorOf(Query.TestActor.Props("b"));
            
            a.Tell("hello");
            await ExpectMsgAsync("hello-done");
            b.Tell("a black car");
            await ExpectMsgAsync("a black car-done");
            a.Tell("something else");
            await ExpectMsgAsync("something else-done");
            a.Tell("a green banana");
            await ExpectMsgAsync("a green banana-done");
            a.Tell("a duplicated apple"); // will emit 2 events
            await ExpectMsgAsync("a duplicated apple-done");
            b.Tell("a green leaf");
            await ExpectMsgAsync("a green leaf-done");
            b.Tell("a repeated green leaf");
            await ExpectMsgAsync("a repeated green leaf-done");
            b.Tell("a repeated green leaf");
            await ExpectMsgAsync("a repeated green leaf-done");
            
            var reader = ReadJournal;
            var probe = reader.CurrentAllEvents(Offset.NoOffset())
                .RunWith(this.SinkProbe<EventEnvelope>(), _materializer);

            await probe.ExpectSubscriptionAsync().ShouldCompleteWithin(1.Seconds());
            await probe.RequestAsync(10);
        
            await ValidateRepresentation(probe, "a", 1L, "hello");
            await ValidateRepresentation(probe, "b", 1L, "a black car");
            await ValidateRepresentation(probe, "a", 2L, "something else");
            await ValidateRepresentation(probe, "a", 3L, "a green banana");
            await ValidateRepresentation(probe, "a", 4L, "a duplicated apple-1");
            await ValidateRepresentation(probe, "a", 4L, "a duplicated apple-2");
            await ValidateRepresentation(probe, "b", 2L, "a green leaf");
            await ValidateRepresentation(probe, "b", 3L, "a repeated green leaf");
            await ValidateRepresentation(probe, "b", 4L, "a repeated green leaf");
            await probe.ExpectCompleteAsync();
        }
        
        private static async Task ValidateRepresentation(TestSubscriber.Probe<EventEnvelope> p, string persistenceId, long sequenceNr, object payload)
        {
            var next = await p.ExpectNextAsync(3.Seconds());
            next.PersistenceId.Should().Be(persistenceId);
            next.SequenceNr.Should().Be(sequenceNr);
            next.Event.Should().Be(payload);
        }
    }
}
