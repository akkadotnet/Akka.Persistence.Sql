// -----------------------------------------------------------------------
//  <copyright file="DataCompatibilitySpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Hosting;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Compat.Common;
using Akka.Persistence.Sql.Data.Compatibility.Tests.Internal;
using Akka.Persistence.Sql.Query;
using Akka.Streams;
using Akka.Streams.Dsl;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests
{
    // Default compatibility spec, logical delete turned off
    public abstract class DataCompatibilitySpec<T> : DataCompatibilitySpecBase<T> where T : ITestContainer, new()
    {
        protected DataCompatibilitySpec(ITestOutputHelper output) : base(output) { }

        protected override void Setup(AkkaConfigurationBuilder builder, IServiceProvider provider) { }

        [Fact(DisplayName = "Linq2Db should recover data created using other persistence plugins")]
        public async Task RecoveryValidationTest()
            => await ValidateRecovery(TestCluster!.ShardRegions[0]);

        [Fact(DisplayName = "Linq2Db should be able to retrieve persistence ids on data created by other persistence plugin")]
        public async Task PersistenceIdQueryValidationTest()
        {
            var system = TestCluster!.ActorSystems[0];

            var readJournal = PersistenceQuery
                .Get(system)
                .ReadJournalFor<Linq2DbReadJournal>(Linq2DbReadJournal.Identifier);

            var result = await readJournal
                .CurrentPersistenceIds()
                .Where(id => !id.StartsWith("/"))
                .RunAsAsyncEnumerable(system.Materializer())
                .ToListAsync();

            result.Should().BeEquivalentTo(Enumerable.Range(0, 100).Select(i => i.ToString()));
        }

        [Fact(DisplayName = "Linq2Db should be able to query events by persistence id on data created by other persistence plugin")]
        public async Task ByPersistenceIdQueryValidationTest()
        {
            var system = TestCluster!.ActorSystems[0];
            var readJournal = PersistenceQuery
                .Get(system)
                .ReadJournalFor<Linq2DbReadJournal>(Linq2DbReadJournal.Identifier);

            foreach (var id in Enumerable.Range(0, 100))
            {
                var baseValue = id * 3;
                var roundTotal = (baseValue * 3 + 3) * 4;

                var events = await readJournal
                    .CurrentEventsByPersistenceId(id.ToString(), 0, long.MaxValue)
                    .RunAsAsyncEnumerable(system.Materializer())
                    .ToListAsync();

                var list = events.Select(
                    env =>
                        env.Event switch
                        {
                            int i => i,
                            string str => int.Parse(str),
                            ShardedMessage msg => msg.Message,
                            CustomShardedMessage msg => msg.Message,
                            _ => throw new Exception("Unknown type")
                        }).ToList();

                list.Count.Should().Be(24);
                var total = list.Aggregate(0, (accumulator, val) => accumulator + val);
                total.Should().Be(roundTotal * 2);
            }
        }

        [Fact(DisplayName = "Linq2Db should be able to query events by tag on data created by other persistence plugin")]
        public async Task TagQueryValidationTest()
            => await ValidateTags(TestCluster!.ActorSystems[0], 2);

        [Fact(DisplayName = "Linq2Db query events by tag should correctly read tags after event deletion")]
        public async Task TagQueryEventDeletionValidationTest()
        {
            // Wake up all entities
            await ValidateRecovery(TestCluster!.ShardRegions[0]);

            // Truncate all entity events
            await TruncateEventsToLastSnapshot();

            // Assert that events were deleted
            var system = TestCluster!.ActorSystems[0];
            var readJournal = PersistenceQuery
                .Get(system)
                .ReadJournalFor<Linq2DbReadJournal>(Linq2DbReadJournal.Identifier);

            foreach (var id in Enumerable.Range(0, 100))
            {
                var baseValue = id * 3;
                var roundTotal = (baseValue * 3 + 3) * 4;

                var events = await readJournal
                    .CurrentEventsByPersistenceId(id.ToString(), 0, long.MaxValue)
                    .RunAsAsyncEnumerable(system.Materializer())
                    .ToListAsync();

                var list = events.Select(
                    env =>
                        env.Event switch
                        {
                            int i => i,
                            string str => int.Parse(str),
                            ShardedMessage msg => msg.Message,
                            CustomShardedMessage msg => msg.Message,
                            _ => throw new Exception("Unknown type")
                        }).ToList();

                list.Count.Should().Be(12);
                var total = list.Aggregate(0, (accumulator, val) => accumulator + val);
                total.Should().Be(roundTotal);
            }

            // Assert that events are preserved in the database (soft delete) and are still readable by the tag query
            await ValidateTags(TestCluster!.ActorSystems[0], 1);
        }
    }
}
