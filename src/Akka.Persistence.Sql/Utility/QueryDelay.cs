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
        var completion = new TaskCompletionSource<NotUsed>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var scheduled = new Cancelable(scheduler);
        using var registration = token.Register(() =>
        {
            scheduled.Cancel();
            completion.TrySetCanceled(token);
        });

        try
        {
            scheduler.Advanced.ScheduleOnce(duration, () => completion.TrySetResult(NotUsed.Instance), scheduled);
            await completion.Task.ConfigureAwait(false);
        }
        finally
        {
            scheduled.Cancel();
        }
    }
}
