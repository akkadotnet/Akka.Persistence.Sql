// -----------------------------------------------------------------------
//  <copyright file="WriteQueueMode.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Config
{
    /// <summary>
    /// Selects the pipeline that buffers and batches journal writes before they are sent to the database.
    /// Configured with <c>write-queue-mode</c>.
    /// </summary>
    public enum WriteQueueMode
    {
        /// <summary>
        /// <c>source-queue</c>: an Akka.Streams <c>Source.Queue</c> followed by <c>BatchWeighted</c>.
        /// This is the default.
        /// </summary>
        SourceQueue,

        /// <summary>
        /// <c>channel</c>: a bounded <c>System.Threading.Channels</c> channel that is batched on read
        /// and fed to the stream through <c>Source.ChannelReader</c>. Batches are accumulated in place,
        /// which avoids the per-write allocations of <c>BatchWeighted</c> with immutable batches.
        /// </summary>
        Channel,
    }

    internal static class WriteQueueModeExtensions
    {
        public static WriteQueueMode ParseWriteQueueMode(string? value)
            => value?.Trim().ToLowerInvariant() switch
            {
                null or "" or "source-queue" => WriteQueueMode.SourceQueue,
                "channel" => WriteQueueMode.Channel,
                _ => throw new ArgumentException(
                    $"Invalid write-queue-mode '{value}'. Valid values are 'source-queue' and 'channel'.",
                    nameof(value)),
            };
    }
}
