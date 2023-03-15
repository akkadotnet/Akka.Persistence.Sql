// -----------------------------------------------------------------------
//  <copyright file="TestCluster.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Cluster.Hosting;
using Akka.Cluster.Sharding;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Akka.Persistence.Sql.Compat.Common;
using Akka.Remote.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.Internal
{
    public sealed class TestCluster : IAsyncDisposable
    {
        private readonly TimeSpan _clusterStartTimeout;

        private readonly IHost _host1;
        private readonly IHost _host2;
        private readonly IHost _host3;

        public TestCluster(
            Action<AkkaConfigurationBuilder, IServiceProvider> setup,
            string journalId,
            ITestOutputHelper helper,
            float clusterStartTimeoutInSeconds = 10)
        {
            _clusterStartTimeout = TimeSpan.FromSeconds(clusterStartTimeoutInSeconds);

            _host1 = CreateHost(setup, 12552, journalId, helper);
            _host2 = CreateHost(setup, 12553, journalId, helper);
            _host3 = CreateHost(setup, 12554, journalId, helper);

            Hosts = ImmutableList.Create(_host1, _host2, _host3);
        }

        public bool IsStarted => ShardRegions.Count > 0;

        public ImmutableList<IActorRef> ShardRegions { get; private set; } = ImmutableList<IActorRef>.Empty;

        public ImmutableList<IHost> Hosts { get; }

        public ImmutableList<ActorSystem> ActorSystems { get; private set; } = ImmutableList<ActorSystem>.Empty;

        public async ValueTask DisposeAsync()
            => await ShutdownAsync();

        public async Task StartAsync(CancellationToken token = default)
        {
            await Task.WhenAll(
                _host1.StartAsync(token),
                _host2.StartAsync(token),
                _host3.StartAsync(token)
            );

            await StartClusterAsync(token);

            ShardRegions = ImmutableList.Create(
                await _host1.Services.GetRequiredService<ActorRegistry>().GetAsync<ShardRegion>(token),
                await _host2.Services.GetRequiredService<ActorRegistry>().GetAsync<ShardRegion>(token),
                await _host3.Services.GetRequiredService<ActorRegistry>().GetAsync<ShardRegion>(token));

            ActorSystems = ImmutableList.Create(
                _host1.Services.GetRequiredService<ActorSystem>(),
                _host2.Services.GetRequiredService<ActorSystem>(),
                _host3.Services.GetRequiredService<ActorSystem>());
        }

        private static IHost CreateHost(
            Action<AkkaConfigurationBuilder, IServiceProvider> setup,
            int port,
            string journalId,
            ITestOutputHelper helper)
            => new HostBuilder()
                .ConfigureLogging(
                    logger =>
                    {
                        logger.ClearProviders();
                        logger.AddProvider(new XUnitLoggerProvider(helper, LogLevel.Information));
                        logger.AddFilter("Akka.*", LogLevel.Information);
                    })
                .ConfigureServices(
                    (_, services) =>
                    {
                        services.AddAkka(
                            "TestSystem",
                            (builder, provider) =>
                            {
                                builder
                                    .ConfigureLoggers(
                                        logger =>
                                        {
                                            logger.LogLevel = Event.LogLevel.InfoLevel;
                                            logger.AddLoggerFactory();
                                        })
                                    .AddHocon(
                                        @"
akka.cluster.min-nr-of-members = 3
akka.cluster.sharding.snapshot-after = 20
akka.actor.ask-timeout = 3s",
                                        HoconAddMode.Prepend)
                                    .WithCustomSerializer(
                                        serializerIdentifier: "customSerializer",
                                        boundTypes: new[] { typeof(CustomShardedMessage) },
                                        serializerFactory: system => new CustomSerializer(system))
                                    .WithRemoting("localhost", port)
                                    .WithClustering()
                                    .WithShardRegion<ShardRegion>(
                                        "test",
                                        EntityActor.Props,
                                        new MessageExtractor(),
                                        new ShardOptions
                                        {
                                            RememberEntities = false,
                                            StateStoreMode = StateStoreMode.Persistence
                                        })
                                    .WithJournal(
                                        journalId,
                                        journalBuilder =>
                                        {
                                            journalBuilder.AddWriteEventAdapter<EventAdapter>(
                                                eventAdapterName: "customMessage",
                                                boundTypes: new[] { typeof(int), typeof(string) });
                                        });

                                setup(builder, provider);
                            });
                    }).Build();

        [SuppressMessage("ReSharper", "MethodHasAsyncOverloadWithCancellation")]
        private async Task StartClusterAsync(CancellationToken token)
        {
            var node1 = _host1.Services.GetRequiredService<ActorSystem>();
            var node2 = _host2.Services.GetRequiredService<ActorSystem>();
            var node3 = _host3.Services.GetRequiredService<ActorSystem>();

            var clusterTcs = new TaskCompletionSource<Done>();

            // We're not using JoinAsync, this method is doing what JoinAsync would, but with multiple nodes at once.
            var cluster = Cluster.Cluster.Get(node1);
            var address = cluster.SelfAddress;
            cluster.RegisterOnMemberUp(() => clusterTcs.SetResult(Done.Instance));
            cluster.Join(address);
            Cluster.Cluster.Get(node2).Join(address);
            Cluster.Cluster.Get(node3).Join(address);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            cts.CancelAfter(_clusterStartTimeout);
            var task = await Task.WhenAny(clusterTcs.Task, Task.Delay(Timeout.Infinite, cts.Token));
            if (task != clusterTcs.Task)
            {
                await ShutdownAsync();
                throw new TimeoutException($"Cluster failed to form in {_clusterStartTimeout.TotalSeconds} seconds");
            }

            cts.Cancel();

            // wait 2 seconds for everything to settle down
            await Task.Delay(2000, token);
        }

        private async Task ShutdownAsync()
        {
            void DisposeHost(IDisposable host, ICollection<Task> tasks)
            {
                if (host is IAsyncDisposable asyncHost)
                {
                    tasks.Add(asyncHost.DisposeAsync().AsTask());
                }
                else
                {
                    host.Dispose();
                }
            }

            var tasks = new List<Task>();

            DisposeHost(_host1, tasks);
            DisposeHost(_host2, tasks);
            DisposeHost(_host3, tasks);

            if (tasks.Count > 0)
                await Task.WhenAll(tasks);
        }
    }
}
