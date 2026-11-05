// -----------------------------------------------------------------------
//  <copyright file="SqlJournalPerfSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Routing;
using Akka.TestKit;
using Akka.Util;
using Akka.Util.Internal;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;
using MathNet.Numerics.Statistics;
using Xunit;

namespace Akka.Persistence.Sql.Benchmark.Tests
{
    public abstract class SqlJournalPerfSpec<T> : Akka.TestKit.Xunit.TestKit where T : ITestContainer
    {
        // Number of measurement iterations each test will be run.
        private const int MeasurementIterations = 101;
        private const double OutlierRejectionSigma = 2;

        // Number of messages sent to the PersistentActor under test for each test iteration
        private readonly int _eventsCount;

        private readonly TimeSpan _expectDuration;
        private readonly TestProbe _testProbe;

        /// <summary>
        ///     Optional data blob attached to every <see cref="Cmd"/> to simulate realistic payload sizes.
        ///     Generated once and shared across all commands to avoid allocation noise in benchmarks.
        /// </summary>
        private readonly byte[]? _payload;

        /// <summary>
        ///     Time from handing each event to the journal until its persist callback runs,
        ///     collected during the measured iterations only.
        /// </summary>
        private readonly LatencyRecorder _latency = new();

        protected SqlJournalPerfSpec(
            Configuration.Config? config,
            string actorSystem,
            ITestOutputHelper output,
            int timeoutDurationSeconds = 30,
            int eventsCount = 10000,
            int payloadSizeBytes = 0)
            : base(WithWriteQueueMode(config ?? Configuration.Config.Empty), actorSystem, output)
        {
            ThreadPool.SetMinThreads(12, 12);
            Output.WriteLine($"write-queue-mode = {Sys.Settings.Config.GetString("akka.persistence.journal.sql.write-queue-mode", "source-queue")}");
            _eventsCount = eventsCount;
            _expectDuration = TimeSpan.FromSeconds(timeoutDurationSeconds);
            _testProbe = CreateTestProbe();
            _commands = Enumerable.Range(1, _eventsCount).ToArray();

            if (payloadSizeBytes > 0)
            {
                _payload = new byte[payloadSizeBytes];
                Random.Shared.NextBytes(_payload);
            }
        }

        private readonly IReadOnlyList<int> _commands; 

        /// <summary>
        ///     Environment variable that sets <c>write-queue-mode</c> (<c>source-queue</c> or <c>channel</c>)
        ///     for every perf spec, so both write queues can be measured with otherwise identical settings.
        ///     When unset, the spec's own configuration (normally the default, <c>source-queue</c>) is used.
        /// </summary>
        public const string WriteQueueModeEnvironmentVariable = "AKKA_PERSISTENCE_SQL_BENCH_WRITE_QUEUE_MODE";

        /// <summary>
        ///     Environment variable naming a directory that receives the raw samples of every measurement
        ///     as CSV files (iteration times and per-event latencies). When unset, no files are written.
        /// </summary>
        public const string OutputDirectoryEnvironmentVariable = "AKKA_PERSISTENCE_SQL_BENCH_OUTPUT_DIR";

        /// <summary>
        ///     Environment variable that sets the total send rate (events per second) of the fixed-rate latency test.
        /// </summary>
        public const string LatencyRateEnvironmentVariable = "AKKA_PERSISTENCE_SQL_BENCH_LATENCY_RATE";

        /// <summary>
        ///     Environment variable that sets how many seconds the fixed-rate latency test measures for.
        /// </summary>
        public const string LatencySecondsEnvironmentVariable = "AKKA_PERSISTENCE_SQL_BENCH_LATENCY_SECONDS";

        private static Configuration.Config WithWriteQueueMode(Configuration.Config config)
        {
            var mode = Environment.GetEnvironmentVariable(WriteQueueModeEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(mode))
                return config;

            return ConfigurationFactory
                .ParseString($"akka.persistence.journal.sql.write-queue-mode = {mode.Trim()}")
                .WithFallback(config);
        }

        internal IActorRef BenchActor(string pid, int replyAfter)
            => Sys.ActorOf(Props.Create(() => new BenchActor(pid, _testProbe, _eventsCount, _latency)));

        internal (IActorRef aut, TestProbe probe) BenchActorNewProbe(string pid, int replyAfter)
        {
            var tp = CreateTestProbe();
            return (Sys.ActorOf(Props.Create(() => new BenchActor(pid, tp, _eventsCount, _latency))), tp);
        }

        internal (IActorRef aut, TestProbe probe) BenchActorNewProbeGroup(string pid, int numActors, int numMessages)
        {
            var tp = CreateTestProbe();
            return (Sys.ActorOf(
                Props
                    .Create(() => new BenchActor(pid, tp, numMessages, _latency, false))
                    .WithRouter(new RoundRobinPool(numActors))), tp);
        }

        internal async Task FeedAndExpectLastRouterSetAsync(
            (IActorRef actor, TestProbe probe) autSet,
            string mode,
            IReadOnlyList<int> commands,
            int numExpect)
        {
            commands.ForEach(c => autSet.actor.Tell(new Broadcast(new Cmd(mode, c, _payload))));

            for (var i = 0; i < numExpect; i++)
                await autSet.probe.ExpectMsgAsync(commands[^1], _expectDuration);
        }

        internal async Task FeedAndExpectLastAsync(IActorRef actor, string mode, IReadOnlyList<int> commands)
        {
            commands.ForEach(c => actor.Tell(new Cmd(mode, c, _payload)));
            await _testProbe.ExpectMsgAsync(commands[^1], _expectDuration);
        }

        internal async Task FeedAndExpectLastGroupAsync(
            (IActorRef actor, TestProbe probe)[] autSet,
            string mode,
            IReadOnlyList<int> commands)
        {
            foreach (var (actor, _) in autSet)
                commands.ForEach(c => actor.Tell(new Cmd(mode, c, _payload)));

            foreach (var (_, probe) in autSet)
                await probe.ExpectMsgAsync(commands[^1], _expectDuration);
        }

        internal async Task FeedAndExpectLastSpecificAsync(
            (IActorRef actor, TestProbe probe) aut,
            string mode,
            IReadOnlyList<int> commands)
        {
            commands.ForEach(c => aut.actor.Tell(new Cmd(mode, c, _payload)));

            await aut.probe.ExpectMsgAsync(commands[^1], _expectDuration);
        }

        internal async Task MeasureAsync(Func<TimeSpan, string> msg, Func<Task> block)
        {
            var measurements = new List<TimeSpan>(MeasurementIterations);

            await block(); // warm-up

            _latency.Start(_eventsCount * MeasurementIterations);
            var i = 0;
            while (i < MeasurementIterations)
            {
                var sw = Stopwatch.StartNew();
                await block();
                sw.Stop();
                measurements.Add(sw.Elapsed);
                Output.WriteLine(msg(sw.Elapsed));
                i++;
            }

            var latencies = _latency.Stop();

            var (rejected, times) = RejectOutliers(measurements.Select(c => c.TotalMilliseconds).ToArray(), OutlierRejectionSigma);

            var mean = times.Average();
            var stdDev = times.PopulationStandardDeviation();
            var min = times.Minimum();
            var q1 = times.LowerQuartile();
            var median = times.Median();
            var q3 = times.UpperQuartile();
            var max = times.Maximum();
            
            Output.WriteLine($"Mean: {mean:F2} ms, Standard Deviation: {stdDev:F2} ms, Min: {min:F2} ms, Q1: {q1:F2} ms, Median: {median:F2} ms, Q3: {q3:F2} ms, Max: {max:F2} ms");

            var msgPerSec = _eventsCount / mean * 1000;
            Output.WriteLine($"Mean throughput: {msgPerSec:F2} msg/s");
            
            var medianMsgPerSec = _eventsCount / median * 1000;
            Output.WriteLine($"Median throughput: {medianMsgPerSec:F2} msg/s");
            
            Output.WriteLine($"Rejected outlier (sigma: {OutlierRejectionSigma}): {string.Join(", ", rejected)}");

            ReportLatency(latencies);
            WriteRawSamples(measurements, latencies);
        }

        internal async Task MeasureGroupAsync(Func<TimeSpan, string> msg, Func<Task> block, int numMsg, int numGroup)
        {
            var measurements = new List<TimeSpan>(MeasurementIterations);

            await block();
            await block(); // warm-up

            _latency.Start(numMsg * numGroup * MeasurementIterations);
            var i = 0;
            while (i < MeasurementIterations)
            {
                var sw = Stopwatch.StartNew();
                await block();
                sw.Stop();
                measurements.Add(sw.Elapsed);
                Output.WriteLine(msg(sw.Elapsed));
                i++;
            }

            var latencies = _latency.Stop();

            var (rejected, times) = RejectOutliers(measurements.Select(c => c.TotalMilliseconds).ToArray(), OutlierRejectionSigma);

            var mean = times.Average();
            var stdDev = times.PopulationStandardDeviation();
            var min = times.Minimum();
            var q1 = times.LowerQuartile();
            var median = times.Median();
            var q3 = times.UpperQuartile();
            var max = times.Maximum();
            
            Output.WriteLine($"Workers: {numGroup}, Mean: {mean:F2} ms, Standard Deviation: {stdDev:F2} ms, Min: {min:F2} ms, Q1: {q1:F2} ms, Median: {median:F2} ms, Q3: {q3:F2} ms, Max: {max:F2} ms");

            var msgPerSec = numMsg / mean * 1000;
            var msgPerSecTotal = numMsg * numGroup / mean * 1000;
            
            Output.WriteLine($"Mean throughput: {msgPerSec:F2} msg/s/actor, Mean total throughput: {msgPerSecTotal:F2} msg/s");
            
            var medianMsgPerSec = numMsg / median * 1000;
            var medianMsgPerSecTotal = numMsg * numGroup / median * 1000;
            Output.WriteLine($"Median throughput: {medianMsgPerSec:F2} msg/s/actor, Median total throughput: {medianMsgPerSecTotal:F2} msg/s");
            
            Output.WriteLine($"Rejected outlier (sigma: {OutlierRejectionSigma}): {string.Join(", ", rejected)}");

            ReportLatency(latencies);
            WriteRawSamples(measurements, latencies);
        }

        private void ReportLatency(long[] latencies)
        {
            if (latencies.Length == 0)
                return;

            var sorted = (long[])latencies.Clone();
            Array.Sort(sorted);

            Output.WriteLine(
                $"Latency ({sorted.Length} events): p50: {ToMilliseconds(Percentile(sorted, 0.5)):F3} ms, " +
                $"p90: {ToMilliseconds(Percentile(sorted, 0.9)):F3} ms, p99: {ToMilliseconds(Percentile(sorted, 0.99)):F3} ms, " +
                $"p99.9: {ToMilliseconds(Percentile(sorted, 0.999)):F3} ms, Max: {ToMilliseconds(sorted[^1]):F3} ms");
        }

        private static long Percentile(long[] sorted, double percentile)
            => sorted[Math.Clamp((int)Math.Ceiling(percentile * sorted.Length) - 1, 0, sorted.Length - 1)];

        private static double ToMilliseconds(long stopwatchTicks)
            => stopwatchTicks * 1000.0 / Stopwatch.Frequency;

        private void WriteRawSamples(IReadOnlyList<TimeSpan>? iterations, long[] latencies)
        {
            var directory = Environment.GetEnvironmentVariable(OutputDirectoryEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(directory))
                return;

            Directory.CreateDirectory(directory);
            var mode = Sys.Settings.Config.GetString("akka.persistence.journal.sql.write-queue-mode", "source-queue");
            var test = TestContext.Current.TestMethod?.MethodName ?? "unknown";
            var prefix = Path.Combine(
                directory,
                $"{Sys.Name}.{test}.{mode}.{DateTime.UtcNow.ToString("yyyyMMddTHHmmss", CultureInfo.InvariantCulture)}");

            if (iterations is not null)
            {
                File.WriteAllLines(
                    prefix + ".iterations.csv",
                    iterations
                        .Select(t => t.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture))
                        .Prepend("iteration_ms"));
            }

            File.WriteAllLines(
                prefix + ".latency.csv",
                latencies
                    .Select(t => ToMilliseconds(t).ToString("F3", CultureInfo.InvariantCulture))
                    .Prepend("latency_ms"));

            Output.WriteLine($"Raw samples written to {prefix}.*.csv");
        }

        private static (IReadOnlyList<double> Rejected, IReadOnlyList<double> Measurements) RejectOutliers(IReadOnlyList<double> measurements, double sigma)
        {
            var mean = measurements.Average();
            var stdDev = measurements.PopulationStandardDeviation();
            var threshold = sigma * stdDev;
            var minThreshold = mean - threshold;
            var maxThreshold = mean + threshold;
            var rejected = measurements.Where(m => m < minThreshold || m > maxThreshold);
            var accepted = measurements.Where(m => m >= minThreshold && m <= maxThreshold);
            return (rejected.ToArray(), accepted.ToArray());
        }

        [DotMemoryUnit(CollectAllocations = true, FailIfRunWithoutSupport = false)]
        [Fact]
        public void DotMemory_PersistenceActor_performance_must_measure_Persist()
        {
            dotMemory.Check();

            var p1 = BenchActor("DotMemoryPersistPid", _eventsCount);

            dotMemory.Check(
                _ =>
                {
#pragma warning disable xUnit1031
                    MeasureAsync(
                        d => $"Persist()-ing {_eventsCount} took {d.TotalMilliseconds} ms",
                        async () =>
                        {
                            await FeedAndExpectLastAsync(p1, "p", _commands);
                            p1.Tell(ResetCounter.Instance);
                        }).GetAwaiter().GetResult();
#pragma warning restore xUnit1031
                }
            );

            dotMemory.Check(
                _ =>
                {
#pragma warning disable xUnit1031
                    MeasureAsync(
                        d => $"Persist()-ing {_eventsCount} took {d.TotalMilliseconds} ms",
                        async () =>
                        {
                            await FeedAndExpectLastAsync(p1, "p", _commands);
                            p1.Tell(ResetCounter.Instance);
                        }).GetAwaiter().GetResult();
#pragma warning restore xUnit1031
                }
            );

            dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }

        [DotMemoryUnit(CollectAllocations = true, FailIfRunWithoutSupport = false)]
        [Fact]
        public void DotMemory_PersistenceActor_performance_must_measure_PersistGroup400()
        {
            dotMemory.Check();

            const int numGroup = 400;
            var numCommands = Math.Min(_eventsCount / 100, 500);

            dotMemory.Check(
                _ =>
                {
#pragma warning disable xUnit1031
                    RunGroupBenchmarkAsync(numGroup, numCommands).GetAwaiter().GetResult();
#pragma warning restore xUnit1031
                }
            );

            dotMemory.Check(
                _ =>
                {
#pragma warning disable xUnit1031
                    RunGroupBenchmarkAsync(numGroup, numCommands).GetAwaiter().GetResult();
#pragma warning restore xUnit1031
                }
            );

            dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }

        [Fact]
        public async Task PersistenceActor_performance_must_measure_Persist()
        {
            var p1 = BenchActor("PersistPid", _eventsCount);

            //dotMemory.Check((mem) =>
            //{
            await MeasureAsync(
                d =>
                    $"Persist()-ing {_eventsCount} took {d.TotalMilliseconds} ms",
                async () =>
                {
                    await FeedAndExpectLastAsync(p1, "p", _commands);
                    p1.Tell(ResetCounter.Instance);
                });
            //}
            //);
            //dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }

        /*
        [DotMemoryUnit(CollectAllocations=true, FailIfRunWithoutSupport = false)]
        [Fact]
        public void PersistenceActor_performance_must_measure_PersistDouble()
        {
            //  dotMemory.Check();

            var p1 = BenchActorNewProbe("DoublePersistPid1", EventsCount);
            var p2 = BenchActorNewProbe("DoublePersistPid2", EventsCount);
            //dotMemory.Check((mem) =>
            {
                Measure(
                    d =>
                        $"Persist()-ing {EventsCount} took {d.TotalMilliseconds} ms",
                    () =>
                    {
                        var t1 = Task.Run(() => FeedAndExpectLastSpecific(p1, "p", Commands));
                        var t2 = Task.Run(()=>FeedAndExpectLastSpecific(p2, "p", Commands));
                        Task.WhenAll(new[] {t1, t2}).Wait();
                        p1.aut.Tell(ResetCounter.Instance);
                        p2.aut.Tell(ResetCounter.Instance);
                    });
            }
            //);
            //dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_PersistTriple()
        {

            //  dotMemory.Check();

            var p1 = BenchActorNewProbe("TriplePersistPid1", EventsCount);
            var p2 = BenchActorNewProbe("TriplePersistPid2", EventsCount);
            var p3 = BenchActorNewProbe("TriplePersistPid3", EventsCount);
            //dotMemory.Check((mem) =>
            {
                Measure(
                    d =>
                        $"Persist()-ing {EventsCount} took {d.TotalMilliseconds} ms",
                    () =>
                    {
                        var t1 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p1, "p", Commands));
                        var t2 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p2, "p", Commands));
                        var t3 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p3, "p", Commands));
                        Task.WhenAll(new[] {t1, t2, t3}).Wait();
                        p1.aut.Tell(ResetCounter.Instance);
                        p2.aut.Tell(ResetCounter.Instance);
                        p3.aut.Tell(ResetCounter.Instance);
                    });
            }
            //);
            //dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }
        */

        [Fact]
        public async Task PersistenceActor_performance_must_measure_PersistGroup10()
        {
            const int numGroup = 10;
            var numCommands = Math.Min(_eventsCount / 10, 1000);
            await RunGroupBenchmarkAsync(numGroup, numCommands);
        }

        [Fact]
        public async Task PersistenceActor_performance_must_measure_PersistGroup25()
        {
            const int numGroup = 25;
            var numCommands = Math.Min(_eventsCount / 25, 1000);
            await RunGroupBenchmarkAsync(numGroup, numCommands);
        }

        [Fact]
        public async Task PersistenceActor_performance_must_measure_PersistGroup50()
        {
            const int numGroup = 50;
            var numCommands = Math.Min(_eventsCount / 50, 1000);
            await RunGroupBenchmarkAsync(numGroup, numCommands);
        }

        [Fact]
        public async Task PersistenceActor_performance_must_measure_PersistGroup100()
        {
            const int numGroup = 100;
            var numCommands = Math.Min(_eventsCount / 100, 1000);
            await RunGroupBenchmarkAsync(numGroup, numCommands);
        }

        [Fact]
        public async Task PersistenceActor_performance_must_measure_PersistGroup200()
        {
            const int numGroup = 200;
            var numCommands = Math.Min(_eventsCount / 100, 500);
            await RunGroupBenchmarkAsync(numGroup, numCommands);
        }

        [Fact]
        public async Task PersistenceActor_performance_must_measure_PersistGroup400()
        {
            const int numGroup = 400;
            var numCommands = Math.Min(_eventsCount / 100, 500);
            await RunGroupBenchmarkAsync(numGroup, numCommands);
        }

        protected async Task RunGroupBenchmarkAsync(int numGroup, int numCommands)
        {
            var p1 = BenchActorNewProbeGroup("GroupPersistPid" + numGroup, numGroup, numCommands);
            var commands = _commands.Take(numCommands).ToArray();
            await MeasureGroupAsync(
                d => $"Persist()-ing {numCommands} * {numGroup} took {d.TotalMilliseconds} ms",
                async () =>
                {
                    await FeedAndExpectLastRouterSetAsync(
                        p1,
                        "p",
                        commands,
                        numGroup);

                    p1.aut.Tell(new Broadcast(ResetCounter.Instance));
                },
                numCommands,
                numGroup
            );
        }

        /*
        [Fact]
        public void PersistenceActor_performance_must_measure_PersistQuad()
        {
            //  dotMemory.Check();

            var p1 = BenchActorNewProbe("QuadPersistPid1", EventsCount);
            var p2 = BenchActorNewProbe("QuadPersistPid2", EventsCount);
            var p3 = BenchActorNewProbe("QuadPersistPid3", EventsCount);
            var p4 = BenchActorNewProbe("QuadPersistPid4", EventsCount);
            //dotMemory.Check((mem) =>
            {
                Measure(
                    d =>
                        $"Persist()-ing {EventsCount} took {d.TotalMilliseconds} ms",
                    () =>
                    {
                        FeedAndExpectLastGroup(new []{p1,p2,p3,p4},"p", Commands);
                        p1.aut.Tell(ResetCounter.Instance);
                        p2.aut.Tell(ResetCounter.Instance);
                        p3.aut.Tell(ResetCounter.Instance);
                        p4.aut.Tell(ResetCounter.Instance);
                    });
            }
            //);
            //dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_PersistOct()
        {
            //  dotMemory.Check();

            var p1 = BenchActorNewProbe("OctPersistPid1", EventsCount);
            var p2 = BenchActorNewProbe("OctPersistPid2", EventsCount);
            var p3 = BenchActorNewProbe("OctPersistPid3", EventsCount);
            var p4 = BenchActorNewProbe("OctPersistPid4", EventsCount);
            var p5 = BenchActorNewProbe("OctPersistPid5", EventsCount);
            var p6 = BenchActorNewProbe("OctPersistPid6", EventsCount);
            var p7 = BenchActorNewProbe("OctPersistPid7", EventsCount);
            var p8 = BenchActorNewProbe("OctPersistPid8", EventsCount);
            //dotMemory.Check((mem) =>
            {
                Measure(
                    d =>
                        $"Persist()-ing {EventsCount} took {d.TotalMilliseconds} ms",
                    () =>
                    {
                        FeedAndExpectLastGroup(new []{p1,p2,p3,p4,p5,p6,p7,p8}, "p", Commands);
                        p1.aut.Tell(ResetCounter.Instance);
                        p2.aut.Tell(ResetCounter.Instance);
                        p3.aut.Tell(ResetCounter.Instance);
                        p4.aut.Tell(ResetCounter.Instance);
                        p5.aut.Tell(ResetCounter.Instance);
                        p6.aut.Tell(ResetCounter.Instance);
                        p7.aut.Tell(ResetCounter.Instance);
                        p8.aut.Tell(ResetCounter.Instance);
                    });
            }
            //);
            //dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }
        */

        [Fact]
        public async Task PersistenceActor_performance_must_measure_PersistAll()
        {
            var p1 = BenchActor("PersistAllPid", _eventsCount);
            await MeasureAsync(
                d => $"PersistAll()-ing {_eventsCount} took {d.TotalMilliseconds} ms",
                async () =>
                {
                    await FeedAndExpectLastAsync(p1, "pb", _commands);
                    p1.Tell(ResetCounter.Instance);
                });
        }

        [Fact]
        public async Task PersistenceActor_performance_must_measure_PersistAsync()
        {
            var p1 = BenchActor("PersistAsyncPid", _eventsCount);
            await MeasureAsync(
                d => $"PersistAsync()-ing {_eventsCount} took {d.TotalMilliseconds} ms",
                async () =>
                {
                    await FeedAndExpectLastAsync(p1, "pa", _commands);
                    p1.Tell(ResetCounter.Instance);
                });
        }

        [Fact]
        public async Task PersistenceActor_performance_must_measure_PersistAllAsync()
        {
            var p1 = BenchActor("PersistAllAsyncPid", _eventsCount);
            await MeasureAsync(
                d => $"PersistAllAsync()-ing {_eventsCount} took {d.TotalMilliseconds} ms",
                async () =>
                {
                    await FeedAndExpectLastAsync(p1, "pba", _commands);
                    p1.Tell(ResetCounter.Instance);
                });
        }

        /// <summary>
        ///     Sends single <c>Persist()</c> commands to a group of actors at a fixed total rate that should stay
        ///     below the journal's saturation point, and measures each event's latency from the time it was
        ///     scheduled to be sent until its persist callback ran. Latency is measured from the scheduled time
        ///     rather than the actual send, so a late sender does not hide queueing delay.
        /// </summary>
        [Fact]
        public async Task PersistenceActor_performance_must_measure_PersistLatencyAtFixedRate()
        {
            const int numActors = 100;
            var rate = ReadPositiveEnvironmentVariable(LatencyRateEnvironmentVariable, 2000);
            var seconds = ReadPositiveEnvironmentVariable(LatencySecondsEnvironmentVariable, 30);
            var warmUpSeconds = Math.Min(5, seconds);
            var warmUpEvents = rate * warmUpSeconds;
            var totalEvents = rate * (warmUpSeconds + seconds);

            var actors = Enumerable.Range(0, numActors)
                .Select(i => Sys.ActorOf(Props.Create(() => new BenchActor($"FixedRatePid{i}", _testProbe, int.MaxValue, _latency))))
                .ToArray();

            _latency.Start(totalEvents - warmUpEvents);

            var ticksPerEvent = (double)Stopwatch.Frequency / rate;
            var sendStart = Stopwatch.GetTimestamp();
            var measureStart = sendStart + (long)(warmUpEvents * ticksPerEvent);

            // A dedicated spinning thread, because Thread.Sleep and timers are too coarse on Windows.
            var lastSent = await Task.Factory.StartNew(
                () =>
                {
                    for (var n = 0; n < totalEvents; n++)
                    {
                        var due = sendStart + (long)(n * ticksPerEvent);
                        while (Stopwatch.GetTimestamp() < due)
                            Thread.SpinWait(20);

                        actors[n % numActors].Tell(new TimedCmd(n / numActors + 1, due, due >= measureStart, _payload));
                    }

                    return Stopwatch.GetTimestamp();
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            await AwaitConditionAsync(
                () => Task.FromResult(_latency.Count >= totalEvents - warmUpEvents),
                TimeSpan.FromSeconds(seconds + 60),
                TimeSpan.FromMilliseconds(50));

            var latencies = _latency.Stop();
            var sendSeconds = (double)(lastSent - sendStart) / Stopwatch.Frequency;

            Output.WriteLine(
                $"Fixed rate: {numActors} actors, requested {rate} msg/s, achieved send rate {totalEvents / sendSeconds:F2} msg/s, " +
                $"warm-up {warmUpSeconds} s, measured {seconds} s");
            ReportLatency(latencies);
            WriteRawSamples(null, latencies);
        }

        private static int ReadPositiveEnvironmentVariable(string name, int defaultValue)
            => int.TryParse(Environment.GetEnvironmentVariable(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
                ? value
                : defaultValue;

        [Fact]
        public async Task PersistenceActor_performance_must_measure_Recovering()
        {
            var p1 = BenchActor("PersistRecoverPid", _eventsCount);

            await FeedAndExpectLastAsync(p1, "p", _commands);

            await MeasureAsync(
                d => $"Recovering {_eventsCount} took {d.TotalMilliseconds} ms",
                async () =>
                {
                    BenchActor("PersistRecoverPid", _eventsCount);
                    await _testProbe.ExpectMsgAsync(_commands[^1], _expectDuration);
                });
        }

        [Fact]
        public async Task PersistenceActor_performance_must_measure_RecoveringTwo()
        {
            var p1 = BenchActorNewProbe("DoublePersistRecoverPid1", _eventsCount);
            var p2 = BenchActorNewProbe("DoublePersistRecoverPid2", _eventsCount);

            await FeedAndExpectLastSpecificAsync(p1, "p", _commands);
            await FeedAndExpectLastSpecificAsync(p2, "p", _commands);

            await MeasureGroupAsync(
                d => $"Recovering {_eventsCount} took {d.TotalMilliseconds} ms",
                async () =>
                {
                    async Task Task1()
                    {
                        var (_, probe) = BenchActorNewProbe("DoublePersistRecoverPid1", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    async Task Task2()
                    {
                        var (_, probe) = BenchActorNewProbe("DoublePersistRecoverPid2", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    await Task.WhenAll(Task1(), Task2());
                },
                _eventsCount,
                2);
        }

        [Fact]
        public async Task PersistenceActor_performance_must_measure_RecoveringFour()
        {
            var p1 = BenchActorNewProbe("QuadPersistRecoverPid1", _eventsCount);
            var p2 = BenchActorNewProbe("QuadPersistRecoverPid2", _eventsCount);
            var p3 = BenchActorNewProbe("QuadPersistRecoverPid3", _eventsCount);
            var p4 = BenchActorNewProbe("QuadPersistRecoverPid4", _eventsCount);

            await FeedAndExpectLastSpecificAsync(p1, "p", _commands);
            await FeedAndExpectLastSpecificAsync(p2, "p", _commands);
            await FeedAndExpectLastSpecificAsync(p3, "p", _commands);
            await FeedAndExpectLastSpecificAsync(p4, "p", _commands);

            await MeasureGroupAsync(
                d => $"Recovering {_eventsCount} took {d.TotalMilliseconds} ms",
                async () =>
                {
                    async Task Task1()
                    {
                        var (_, probe) = BenchActorNewProbe("QuadPersistRecoverPid1", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    async Task Task2()
                    {
                        var (_, probe) = BenchActorNewProbe("QuadPersistRecoverPid2", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    async Task Task3()
                    {
                        var (_, probe) = BenchActorNewProbe("QuadPersistRecoverPid3", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    async Task Task4()
                    {
                        var (_, probe) = BenchActorNewProbe("QuadPersistRecoverPid4", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    await Task.WhenAll(Task1(), Task2(), Task3(), Task4());
                },
                _eventsCount,
                4);
        }

        [Fact]
        public async Task PersistenceActor_performance_must_measure_Recovering8()
        {
            var p1 = BenchActorNewProbe("OctPersistRecoverPid1", _eventsCount);
            var p2 = BenchActorNewProbe("OctPersistRecoverPid2", _eventsCount);
            var p3 = BenchActorNewProbe("OctPersistRecoverPid3", _eventsCount);
            var p4 = BenchActorNewProbe("OctPersistRecoverPid4", _eventsCount);
            var p5 = BenchActorNewProbe("OctPersistRecoverPid5", _eventsCount);
            var p6 = BenchActorNewProbe("OctPersistRecoverPid6", _eventsCount);
            var p7 = BenchActorNewProbe("OctPersistRecoverPid7", _eventsCount);
            var p8 = BenchActorNewProbe("OctPersistRecoverPid8", _eventsCount);

            await FeedAndExpectLastSpecificAsync(p1, "p", _commands);
            await FeedAndExpectLastSpecificAsync(p2, "p", _commands);
            await FeedAndExpectLastSpecificAsync(p3, "p", _commands);
            await FeedAndExpectLastSpecificAsync(p4, "p", _commands);
            await FeedAndExpectLastSpecificAsync(p5, "p", _commands);
            await FeedAndExpectLastSpecificAsync(p6, "p", _commands);
            await FeedAndExpectLastSpecificAsync(p7, "p", _commands);
            await FeedAndExpectLastSpecificAsync(p8, "p", _commands);

            await MeasureGroupAsync(
                d => $"Recovering {_eventsCount} took {d.TotalMilliseconds} ms , {_eventsCount * 8 / d.TotalMilliseconds * 1000} total msg/sec",
                async () =>
                {
                    async Task Task1()
                    {
                        var (_, probe) = BenchActorNewProbe("OctPersistRecoverPid1", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    async Task Task2()
                    {
                        var (_, probe) = BenchActorNewProbe("OctPersistRecoverPid2", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    async Task Task3()
                    {
                        var (_, probe) = BenchActorNewProbe("OctPersistRecoverPid3", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    async Task Task4()
                    {
                        var (_, probe) = BenchActorNewProbe("OctPersistRecoverPid4", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    async Task Task5()
                    {
                        var (_, probe) = BenchActorNewProbe("OctPersistRecoverPid5", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    async Task Task6()
                    {
                        var (_, probe) = BenchActorNewProbe("OctPersistRecoverPid6", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    async Task Task7()
                    {
                        var (_, probe) = BenchActorNewProbe("OctPersistRecoverPid7", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    async Task Task8()
                    {
                        var (_, probe) = BenchActorNewProbe("OctPersistRecoverPid8", _eventsCount);
                        await probe.ExpectMsgAsync(_commands[^1], _expectDuration);
                    }

                    await Task.WhenAll(Task1(), Task2(), Task3(), Task4(), Task5(), Task6(), Task7(), Task8());
                },
                _eventsCount,
                8);
        }
    }

    internal class ResetCounter
    {
        private ResetCounter() { }
        public static ResetCounter Instance { get; } = new();
    }

    /// <summary>
    ///     A <c>Persist()</c> command for the fixed-rate latency test, carrying the time it was scheduled to be sent.
    /// </summary>
    internal sealed class TimedCmd
    {
        public TimedCmd(int payload, long scheduledTimestamp, bool measured, byte[]? data)
        {
            Payload = payload;
            ScheduledTimestamp = scheduledTimestamp;
            Measured = measured;
            Data = data;
        }

        public int Payload { get; }

        public long ScheduledTimestamp { get; }

        public bool Measured { get; }

        public byte[]? Data { get; }
    }

    /// <summary>
    ///     Collects per-event latencies, in <see cref="Stopwatch"/> ticks, from any number of actors.
    /// </summary>
    internal sealed class LatencyRecorder
    {
        private long[] _samples = Array.Empty<long>();
        private int _count;
        private volatile bool _recording;

        public int Count => Math.Min(Volatile.Read(ref _count), _samples.Length);

        public void Start(int capacity)
        {
            _samples = new long[capacity];
            _count = 0;
            _recording = true;
        }

        public long[] Stop()
        {
            _recording = false;
            return _samples.AsSpan(0, Count).ToArray();
        }

        public void Record(long startTimestamp)
        {
            if (!_recording)
                return;

            var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
            var index = Interlocked.Increment(ref _count) - 1;
            if (index < _samples.Length)
                _samples[index] = elapsed;
        }
    }

    public class Cmd
    {
        public Cmd(string mode, int payload, byte[]? data = null)
        {
            Mode = mode;
            Payload = payload;
            Data = data;
        }

        public string Mode { get; }

        public int Payload { get; }

        /// <summary>
        ///     Optional data blob to simulate realistic message sizes.
        ///     When <c>null</c>, the serialized event is tiny (just the int payload).
        /// </summary>
        public byte[]? Data { get; }
    }

    internal class BenchActor : UntypedPersistentActor
    {
        private const int BatchSize = 50;
        private List<Cmd> _batch = new(BatchSize);
        private int _counter;
        private readonly LatencyRecorder _latency;

        public BenchActor(string persistenceId, IActorRef replyTo, int replyAfter, LatencyRecorder latency, bool groupName)
        {
            PersistenceId = persistenceId + MurmurHash.StringHash(Context.Parent.Path.Name + Context.Self.Path.Name);
            ReplyTo = replyTo;
            ReplyAfter = replyAfter;
            _latency = latency;
        }

        public BenchActor(string persistenceId, IActorRef replyTo, int replyAfter, LatencyRecorder latency)
        {
            PersistenceId = persistenceId;
            ReplyTo = replyTo;
            ReplyAfter = replyAfter;
            _latency = latency;
        }

        public override string PersistenceId { get; }

        public IActorRef ReplyTo { get; }

        public int ReplyAfter { get; }

        protected override void OnRecover(object message)
        {
            switch (message)
            {
                case Cmd c:
                    _counter++;

                    if (c.Payload != _counter)
                        throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{c.Payload}]");

                    if (_counter == ReplyAfter)
                        ReplyTo.Tell(c.Payload);

                    break;
            }
        }

        protected override void OnCommand(object message)
        {
            switch (message)
            {
                case Cmd { Mode: "p" } c:
                {
                    var start = Stopwatch.GetTimestamp();
                    Persist(
                        c,
                        d =>
                        {
                            _latency.Record(start);
                            _counter += 1;
                            if (d.Payload != _counter)
                                throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{d.Payload}]");
                            if (_counter == ReplyAfter)
                                ReplyTo.Tell(d.Payload);
                        });

                    break;
                }

                case TimedCmd t:
                    Persist(
                        new Cmd("p", t.Payload, t.Data),
                        d =>
                        {
                            if (t.Measured)
                                _latency.Record(t.ScheduledTimestamp);
                            _counter += 1;
                            if (d.Payload != _counter)
                                throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{d.Payload}]");
                        });

                    break;

                case Cmd { Mode: "pb" } c:
                    _batch.Add(c);

                    if (_batch.Count % BatchSize == 0)
                    {
                        var start = Stopwatch.GetTimestamp();
                        PersistAll(
                            _batch,
                            d =>
                            {
                                _latency.Record(start);
                                _counter += 1;
                                if (d.Payload != _counter)
                                    throw new ArgumentException(
                                        $"Expected to receive [{_counter}] yet got: [{d.Payload}]");
                                if (_counter == ReplyAfter)
                                    ReplyTo.Tell(d.Payload);
                            });
                        _batch = new List<Cmd>(BatchSize);
                    }

                    break;

                case Cmd { Mode: "pa" } c:
                {
                    var start = Stopwatch.GetTimestamp();
                    PersistAsync(
                        c,
                        d =>
                        {
                            _latency.Record(start);
                            _counter += 1;
                            if (d.Payload != _counter)
                                throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{d.Payload}]");
                            if (_counter == ReplyAfter)
                                ReplyTo.Tell(d.Payload);
                        });

                    break;
                }

                case Cmd { Mode: "pba" } c:
                    _batch.Add(c);

                    if (_batch.Count % BatchSize == 0)
                    {
                        var start = Stopwatch.GetTimestamp();
                        PersistAllAsync(
                            _batch,
                            d =>
                            {
                                _latency.Record(start);
                                _counter += 1;
                                if (d.Payload != _counter)
                                    throw new ArgumentException(
                                        $"Expected to receive [{_counter}] yet got: [{d.Payload}]");
                                if (_counter == ReplyAfter)
                                    ReplyTo.Tell(d.Payload);
                            });
                        _batch = new List<Cmd>(BatchSize);
                    }

                    break;

                case ResetCounter:
                    _counter = 0;
                    break;
            }
        }
    }
}
