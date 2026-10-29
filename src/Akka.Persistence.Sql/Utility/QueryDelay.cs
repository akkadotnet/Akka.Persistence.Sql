// -----------------------------------------------------------------------
//  <copyright file="QueryDelay.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;

namespace Akka.Persistence.Sql.Utility;

internal static class QueryDelay
{
    public static async Task WaitAsync(TimeSpan duration, IScheduler scheduler, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var scheduled = new Cancelable(scheduler);

        try
        {
            scheduler.Advanced.ScheduleOnce(duration, () => completion.TrySetResult(), scheduled);
            await completion.Task.WaitAsync(token).ConfigureAwait(false);
        }
        finally
        {
            scheduled.Cancel();
        }
    }
}
