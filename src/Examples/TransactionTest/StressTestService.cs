// -----------------------------------------------------------------------
//  <copyright file="StressTestService.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Microsoft.Extensions.Hosting;

namespace TransactionTest;

public class StressTestService: IHostedService
{
    private const int TotalActors = 500;
    private const int LargePayloadActorCount = 20;
    private const int LargePayloadSize = 4 * 1024 * 1024;
    private const int SmallPayloadSize = 1024;
    private const int PersistBurstSize = 50;
    
    private readonly Random _random = new();
    private readonly IActorRef?[] _actors = new IActorRef[TotalActors];
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly ActorSystem _system;
    private CancellationTokenSource? _shutdownCts;
    private PeriodicTimer? _timer;
    private Task? _timerTask;
        
    public StressTestService(ActorSystem system, IHostApplicationLifetime applicationLifetime)
    {
        _system = system;
        _applicationLifetime = applicationLifetime;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _shutdownCts = new CancellationTokenSource();
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        _timerTask = Task.Run(
            async () =>
            {
                try
                {
                    while (await _timer.WaitForNextTickAsync(_shutdownCts.Token))
                    {
                        var startIndex = _random.Next(0, TotalActors - PersistBurstSize);
                        foreach (var index in Enumerable.Range(startIndex, PersistBurstSize))
                        {
                            var actor = _actors[index];
                            if (actor is null)
                            {
                                actor = index < LargePayloadActorCount 
                                    ? _system.ActorOf(Props.Create(() => new TestActor($"p-{index}", LargePayloadSize, _applicationLifetime)), $"p-{index}") 
                                    : _system.ActorOf(Props.Create(() => new TestActor($"p-{index}", SmallPayloadSize, _applicationLifetime)), $"p-{index}");
                                await actor.Ask<Initialized>(Initialize.Instance, _shutdownCts.Token);
                                _actors[index] = actor;
                            }
                            actor.Tell(SaveEvent.Instance);
                        }
                    }
                }
                catch (TimeoutException)
                {
                    // no-op
                }
                catch (OperationCanceledException)
                {
                    // no-op
                }
            }, _shutdownCts.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if(_shutdownCts is not null)
            await _shutdownCts.CancelAsync();
        if(_timerTask is not null)
            await _timerTask;
        _timer?.Dispose();
    }
}
