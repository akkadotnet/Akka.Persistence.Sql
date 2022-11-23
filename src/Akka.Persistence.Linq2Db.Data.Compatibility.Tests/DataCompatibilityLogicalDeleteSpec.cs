// -----------------------------------------------------------------------
//  <copyright file="DataCompatibilityLogicalDeleteSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Hosting;
using Akka.Persistence.Linq2Db.Data.Compatibility.Tests.Internal;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Compat.Common;
using Akka.Persistence.Sql.Linq2Db.Query;
using Akka.Streams;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests
{
    // Destructive delete with logical delete turned on
    public abstract class DataCompatibilityLogicalDeleteSpec<T> : DataCompatibilitySpecBase<T> where T : ITestContainer, new()
    {
        protected DataCompatibilityLogicalDeleteSpec(ITestOutputHelper output): base(output)
        {
        }

        protected override void Setup(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            builder.AddHocon((Config)@"
akka.persistence.journal.linq2db.logical-delete = true
akka.persistence.query.journal.linq2db.include-logically-deleted = true", HoconAddMode.Prepend);
        }

        [Fact(DisplayName = "Linq2Db query events by tag should correctly read tags after event deletion")]
        public async Task TagQueryEventDeletionValidationTest()
        {
            // Wake up all entities
            await ValidateRecovery(TestCluster!.ShardRegions[0]);

            // Truncate all entity events
            await TruncateEventsToLastSnapshot();
            
            // Assert that events can still be read
            var system = TestCluster!.ActorSystems[0];
            var readJournal = PersistenceQuery.Get(system)
                .ReadJournalFor<Linq2DbReadJournal>(Linq2DbReadJournal.Identifier);

            foreach (var id in Enumerable.Range(0, 100))
            {
                var baseValue = id * 3;
                var roundTotal = (baseValue * 3 + 3) * 4;

                var events = await readJournal.CurrentEventsByPersistenceId(id.ToString(), 0, long.MaxValue)
                    .RunAsAsyncEnumerable(system.Materializer()).ToListAsync();

                var list = events.Select(env =>
                    env.Event switch
                    {
                        int i => i,
                        string str => int.Parse(str),
                        ShardedMessage msg => msg.Message,
                        CustomShardedMessage msg => msg.Message,
                        _ => throw new Exception("Unknown type")
                    }).ToList();

                list.Count.Should().Be(24);
                var total = list.Aggregate(0, (accum, val) => accum + val);
                total.Should().Be(roundTotal * 2);
            }

            // Assert that events are preserved in the database (soft delete) and are still readable by the tag query
            await ValidateTags(TestCluster!.ActorSystems[0], 2);
        }
    }
}