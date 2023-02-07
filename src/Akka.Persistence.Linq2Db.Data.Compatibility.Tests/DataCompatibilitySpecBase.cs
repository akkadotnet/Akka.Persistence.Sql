// -----------------------------------------------------------------------
//  <copyright file="DataCompatibilitySpecBase.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Hosting;
using Akka.Persistence.Linq2Db.Data.Compatibility.Tests.Internal;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Compat.Common;
using Akka.Persistence.Sql.Linq2Db;
using Akka.Persistence.Sql.Linq2Db.Query;
using Akka.Streams;
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests
{
    public abstract class TestSettings
    {
        public abstract string ProviderName { get; }
        
        public abstract string TableMapping { get; }
        
        public virtual string? SchemaName { get; } = null;
    }
    
    public abstract class DataCompatibilitySpecBase<T>: IAsyncLifetime where T: ITestContainer, new()
    {
        protected TestCluster? TestCluster { get; private set; }
        protected ITestContainer Fixture { get; }
        protected ITestOutputHelper Output { get; }

        protected abstract TestSettings Settings { get; }
        
        protected DataCompatibilitySpecBase(ITestOutputHelper output)
        {
            Output = output;
            Fixture = new T();
        }

        private Config Config()
        {
            return (Config) $@"
akka.persistence {{
	journal {{
		plugin = ""akka.persistence.journal.linq2db""
		linq2db {{
			connection-string = ""{Fixture.ConnectionString}""
			provider-name = {Settings.ProviderName}
			table-mapping = {Settings.TableMapping}
            auto-initialize = off
            warn-on-auto-init-fail = true
            tag-write-mode = Csv

            # Testing for https://github.com/akkadotnet/Akka.Persistence.Linq2Db/pull/117#discussion_r1027345449
            batch-size = 3
            db-round-trip-max-batch-size = 6
            replay-batch-size = 6
{(Settings.SchemaName is { } ? @$"
            {Settings.TableMapping} {{
                schema-name = {Settings.SchemaName}
            }}" : "")}
		}}
	}}

	query.journal.linq2db {{
		connection-string = ""{Fixture.ConnectionString}""
		provider-name = {Settings.ProviderName}
		table-mapping = {Settings.TableMapping}
        auto-initialize = off
        warn-on-auto-init-fail = true
        tag-read-mode = Csv

        # Testing for https://github.com/akkadotnet/Akka.Persistence.Linq2Db/pull/117#discussion_r1027345449
        batch-size = 3
        replay-batch-size = 6
	}}

	snapshot-store {{
		plugin = akka.persistence.snapshot-store.linq2db
		linq2db {{
			connection-string = ""{Fixture.ConnectionString}""
			provider-name = {Settings.ProviderName}
			table-mapping = {Settings.TableMapping}
            auto-initialize = off

{(Settings.SchemaName is { } ? @$"
            {Settings.TableMapping} {{
                schema-name = {Settings.SchemaName}
            }}" : "")}
		}}
	}}
}}";
        }

        protected virtual void CustomSetup(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
        }

        protected abstract void Setup(AkkaConfigurationBuilder builder, IServiceProvider provider);
        
        private void InternalSetup(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.AddHocon(
                Config().WithFallback(Linq2DbPersistence.DefaultConfiguration), 
                HoconAddMode.Prepend);
            Setup(builder, provider);
        }

        public async Task InitializeAsync()
        {
            await Fixture.InitializeAsync();
            TestCluster = new TestCluster(InternalSetup, "linq2db", Output);
            await TestCluster.StartAsync();
        }

        public async Task DisposeAsync()
        {
            if(TestCluster is { })
                await TestCluster.DisposeAsync();
            await Fixture.DisposeAsync();
        }

        protected async Task TruncateEventsToLastSnapshot()
        {
            var region = TestCluster!.ShardRegions[0];
            var cts = new CancellationTokenSource(10.Seconds());
            try
            {
                var tasks = Enumerable.Range(0, 100).Select(id =>
                    region.Ask<(string, StateSnapshot?)>(new Truncate(id), cts.Token)).ToList();
                while (tasks.Count > 0)
                {
                    var task = await Task.WhenAny(tasks);
                    if (cts.Token.IsCancellationRequested)
                        break;

                    tasks.Remove(task);
                    var (id, lastSnapshot) = task.Result;
                    if (lastSnapshot is { })
                        Output.WriteLine(
                            $"{id} data truncated. " +
                            $"Snapshot: [Total: {lastSnapshot.Total}, Persisted: {lastSnapshot.Persisted}]");
                    else
                        throw new XunitException($"Failed to truncate events for entity {id}");
                }

                if (cts.IsCancellationRequested)
                {
                    throw new TimeoutException("Failed to truncate all data within 10 seconds");
                }
            }
            finally
            {
                cts.Dispose();
            }
        }
        
        protected static void ValidateState(string persistentId, StateSnapshot lastSnapshot, int count, int persisted)
        {
            persisted.Should().Be(36, "Entity {0} should have persisted 36 events", persistentId);
            lastSnapshot.Persisted.Should().Be(24, "Entity {0} last snapshot should have persisted 24 events", persistentId);

            var baseValue = int.Parse(persistentId) * 3;
            var roundTotal = (baseValue * 3 + 3) * 4;
            lastSnapshot.Total.Should().Be(roundTotal * 2, "Entity {0} last snapshot total should be {1}", persistentId, roundTotal * 2);
            count.Should().Be(roundTotal * 3, "Entity {0} total should be {1}", persistentId, roundTotal * 3);
        }

        protected static async Task ValidateTags(ActorSystem system, int rounds)
        {
            var readJournal = PersistenceQuery.Get(system)
                .ReadJournalFor<Linq2DbReadJournal>(Linq2DbReadJournal.Identifier);

            // "Tag2" check
            var roundTotal = Enumerable.Range(0, 300)
                .Where(i => i % 3 == 2)
                .Aggregate(0, (accum, val) => accum + val);
            
            var events = await readJournal.CurrentEventsByTag("Tag2", Offset.NoOffset())
                .RunAsAsyncEnumerable(system.Materializer()).ToListAsync();
            events.Count.Should().Be(400 * rounds);

            var intMessages = events.Where(e => e.Event is int).Select(e => (int)e.Event).ToList();
            intMessages.Count.Should().Be(100 * rounds);
            intMessages.Aggregate(0, (accum, val) => accum + val).Should().Be(roundTotal * rounds);
            
            var strMessages = events.Where(e => e.Event is string).Select(e => int.Parse((string)e.Event)).ToList();
            strMessages.Count.Should().Be(100 * rounds);
            strMessages.Aggregate(0, (accum, val) => accum + val).Should().Be(roundTotal * rounds);
            
            var shardMessages = events.Where(e => e.Event is ShardedMessage)
                .Select(e => ((ShardedMessage)e.Event).Message).ToList();
            shardMessages.Count.Should().Be(100 * rounds);
            shardMessages.Aggregate(0, (accum, val) => accum + val).Should().Be(roundTotal * rounds);
            
            var customShardMessages = events.Where(e => e.Event is CustomShardedMessage)
                .Select(e => ((CustomShardedMessage)e.Event).Message).ToList();
            customShardMessages.Count.Should().Be(100 * rounds);
            customShardMessages.Aggregate(0, (accum, val) => accum + val).Should().Be(roundTotal * rounds);
            
            // "Tag1" check, there should be twice as much "Tag1" as "Tag2"
            roundTotal = Enumerable.Range(0, 300)
                .Where(i => i % 3 == 2 || i % 3 == 1)
                .Aggregate(0, (accum, val) => accum + val);
            
            events = await readJournal.CurrentEventsByTag("Tag1", Offset.NoOffset())
                .RunAsAsyncEnumerable(system.Materializer()).ToListAsync();
            events.Count.Should().Be(800 * rounds);
            
            intMessages = events.Where(e => e.Event is int).Select(e => (int)e.Event).ToList();
            intMessages.Count.Should().Be(200 * rounds);
            intMessages.Aggregate(0, (accum, val) => accum + val).Should().Be(roundTotal * rounds);
            
            strMessages = events.Where(e => e.Event is string).Select(e => int.Parse((string)e.Event)).ToList();
            strMessages.Count.Should().Be(200 * rounds);
            strMessages.Aggregate(0, (accum, val) => accum + val).Should().Be(roundTotal * rounds);
            
            shardMessages = events.Where(e => e.Event is ShardedMessage)
                .Select(e => ((ShardedMessage)e.Event).Message).ToList();
            shardMessages.Count.Should().Be(200 * rounds);
            shardMessages.Aggregate(0, (accum, val) => accum + val).Should().Be(roundTotal * rounds);
            
            customShardMessages = events.Where(e => e.Event is CustomShardedMessage)
                .Select(e => ((CustomShardedMessage)e.Event).Message).ToList();
            customShardMessages.Count.Should().Be(200 * rounds);
            customShardMessages.Aggregate(0, (accum, val) => accum + val).Should().Be(roundTotal * rounds);
        }

        protected static async Task ValidateRecovery(IActorRef region)
        {
            foreach (var id in Enumerable.Range(0, 100))
            {
                var (persistenceId, lastSnapshot, total, persisted) =
                    await region.Ask<(string, StateSnapshot, int, int)>(new Start(id));
                ValidateState(persistenceId, lastSnapshot, total, persisted);
            }
        }
    }
}