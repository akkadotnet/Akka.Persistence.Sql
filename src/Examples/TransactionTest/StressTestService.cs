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
    private const int LargePayloadActorCount = 100;
    private const int LargePayloadSize = 5 * 1024 * 1024;
    private const int SmallPayloadSize = 5 * 1024;
    
    private readonly Random _random = new();
    private readonly List<IActorRef> _actors = new();
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
        foreach (var i in Enumerable.Range(0, TotalActors))
        {
            var actor = i < LargePayloadActorCount 
                ? _system.ActorOf(Props.Create(() => new TestActor($"p-{i}", LargePayloadSize, _applicationLifetime)), $"p-{i}") 
                : _system.ActorOf(Props.Create(() => new TestActor($"p-{i}", SmallPayloadSize, _applicationLifetime)), $"p-{i}");
            _actors.Add(actor);
        }

        _shutdownCts = new CancellationTokenSource();
        
        await Task.Delay(TimeSpan.FromSeconds(10), _shutdownCts.Token);
        
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
        _timerTask = Task.Run(
            async () =>
            {
                try
                {
                    while (await _timer.WaitForNextTickAsync(_shutdownCts.Token))
                    {
                        var index = _random.Next(0, TotalActors);
                        _actors[index].Tell(SaveEvent.Instance);
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
    }
}
