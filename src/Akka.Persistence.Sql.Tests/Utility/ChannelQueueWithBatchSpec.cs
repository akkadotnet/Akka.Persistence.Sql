// -----------------------------------------------------------------------
//  <copyright file="ChannelQueueWithBatchSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Utility;
using Akka.Streams;
using Akka.Streams.Dsl;
using FluentAssertions;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Utility;

public class ChannelQueueWithBatchSpec : Akka.TestKit.Xunit.TestKit
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5);

    public ChannelQueueWithBatchSpec(ITestOutputHelper output) : base(output: output) { }

    [Fact]
    public void Batches_items_up_to_the_weight_budget()
    {
        var input = Channel.CreateUnbounded<int>();
        var batcher = CreateBatcher(input.Reader, maxWeight: 2);
        foreach (var i in Enumerable.Range(1, 5))
            input.Writer.TryWrite(i);

        var batches = new List<List<int>>();
        while (batcher.TryRead(out var batch))
            batches.Add(batch);

        batches.Should().BeEquivalentTo(new[] { new[] { 1, 2 }, new[] { 3, 4 }, new[] { 5 } }, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task Completion_waits_until_the_held_back_item_has_been_read()
    {
        var input = Channel.CreateUnbounded<int>();
        var batcher = CreateBatcher(input.Reader, maxWeight: 1);
        input.Writer.TryWrite(1);
        input.Writer.TryWrite(2);
        input.Writer.Complete();

        batcher.TryRead(out var first).Should().BeTrue();
        first.Should().Equal(1);

        // The input is now empty and completed, but item 2 is still held back by the batcher.
        await input.Reader.Completion.WaitAsync(Deadline);
        batcher.Completion.IsCompleted.Should().BeFalse();
        (await batcher.WaitToReadAsync()).Should().BeTrue();

        batcher.TryRead(out var second).Should().BeTrue();
        second.Should().Equal(2);
        await batcher.Completion.WaitAsync(Deadline);
        (await batcher.WaitToReadAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Completion_is_faulted_when_the_input_is_faulted_after_draining()
    {
        var input = Channel.CreateUnbounded<int>();
        var batcher = CreateBatcher(input.Reader, maxWeight: 1);
        input.Writer.TryWrite(1);
        input.Writer.TryWrite(2);
        input.Writer.Complete(new InvalidOperationException("boom"));

        batcher.TryRead(out _).Should().BeTrue();
        await Task.Delay(50);
        batcher.Completion.IsCompleted.Should().BeFalse();
        batcher.TryRead(out _).Should().BeTrue();

        await FluentActions.Awaiting(() => batcher.Completion.WaitAsync(Deadline))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }

    [Fact]
    public async Task ChannelReader_source_delivers_every_batch_when_input_completes_with_an_item_held_back()
    {
        var input = Channel.CreateUnbounded<int>();
        var batcher = CreateBatcher(input.Reader, maxWeight: 1);
        input.Writer.TryWrite(1);
        input.Writer.TryWrite(2);
        input.Writer.Complete();

        var batches = await Source.ChannelReader(batcher)
            .RunWith(Sink.Seq<List<int>>(), Sys.Materializer())
            .WaitAsync(Deadline);

        batches.Should().BeEquivalentTo(new[] { new[] { 1 }, new[] { 2 } }, o => o.WithStrictOrdering());
    }

    private static ChannelQueueWithBatch<int, List<int>> CreateBatcher(ChannelReader<int> reader, long maxWeight)
        => new(
            reader,
            maxWeight,
            static _ => 1,
            static i => new List<int> { i },
            static (batch, i) =>
            {
                batch.Add(i);
                return batch;
            });
}
