﻿//-----------------------------------------------------------------------
// <copyright file="JournalPerfSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2020 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Routing;
using Akka.TestKit;
using Akka.Util;
using Akka.Util.Internal;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Benchmark.Tests
{
    public abstract class L2dbJournalPerfSpec : Akka.TestKit.Xunit2.TestKit
    {
        private readonly TestProbe _testProbe;

        // Number of messages sent to the PersistentActor under test for each test iteration
        private readonly int _eventsCount;

        // Number of measurement iterations each test will be run.
        private const int MeasurementIterations = 10;

        private IReadOnlyList<int> Commands => Enumerable.Range(1, _eventsCount).ToList();

        private readonly TimeSpan _expectDuration;

        protected L2dbJournalPerfSpec(
            Configuration.Config config,
            string actorSystem,
            ITestOutputHelper output,
            int timeoutDurationSeconds = 30,
            int eventsCount = 10000)
            : base(config ?? Configuration.Config.Empty, actorSystem, output)
        {
            ThreadPool.SetMinThreads(12, 12);
            _eventsCount = eventsCount;
            _expectDuration = TimeSpan.FromSeconds(timeoutDurationSeconds);
            _testProbe = CreateTestProbe();
        }

        internal IActorRef BenchActor(string pid, int replyAfter)
        {
            return Sys.ActorOf(Props.Create(() => new BenchActor(pid, _testProbe, _eventsCount)));;
        }

        internal (IActorRef aut,TestProbe probe) BenchActorNewProbe(string pid, int replyAfter)
        {
            var tp = CreateTestProbe();
            return (Sys.ActorOf(Props.Create(() => new BenchActor(pid, tp, _eventsCount))), tp);
        }

        internal (IActorRef aut,TestProbe probe) BenchActorNewProbeGroup(string pid, int numActors, int numMsgs)
        {
            var tp = CreateTestProbe();
            return (
                Sys.ActorOf(Props
                    .Create(() =>
                        new BenchActor(pid, tp, numMsgs, false))
                    .WithRouter(new RoundRobinPool(numActors))), tp);
        }

        internal void FeedAndExpectLastRouterSet(
            (IActorRef actor, TestProbe probe) autSet, string mode, IReadOnlyList<int> commands, int numExpect)
        {
            commands.ForEach(c => autSet.actor.Tell(new Broadcast(new Cmd(mode, c))));

            for (var i = 0; i < numExpect; i++)
            {
                autSet.probe.ExpectMsg(commands.Last(), _expectDuration);
            }
        }

        internal void FeedAndExpectLast(IActorRef actor, string mode, IReadOnlyList<int> commands)
        {
            commands.ForEach(c => actor.Tell(new Cmd(mode, c)));
            _testProbe.ExpectMsg(commands.Last(), _expectDuration);
        }

        internal void FeedAndExpectLastGroup(
            (IActorRef actor, TestProbe probe)[] autSet, string mode, IReadOnlyList<int> commands)
        {
            foreach (var aut in autSet)
            {
                commands.ForEach(c => aut.actor.Tell(new Cmd(mode, c)));
            }

            foreach (var aut in autSet)
            {
                aut.probe.ExpectMsg(commands.Last(), _expectDuration);
            }
        }

        internal void FeedAndExpectLastSpecific((IActorRef actor, TestProbe probe) aut, string mode, IReadOnlyList<int> commands)
        {
            commands.ForEach(c => aut.actor.Tell(new Cmd(mode, c)));

            aut.probe.ExpectMsg(commands.Last(), _expectDuration);
        }

        internal void Measure(Func<TimeSpan, string> msg, Action block)
        {
            var measurements = new List<TimeSpan>(MeasurementIterations);

            block(); //warm-up

            var i = 0;
            while (i < MeasurementIterations)
            {
                var sw = Stopwatch.StartNew();
                block();
                sw.Stop();
                measurements.Add(sw.Elapsed);
                Output.WriteLine(msg(sw.Elapsed));
                i++;
            }

            var avgTime = measurements.Select(c => c.TotalMilliseconds).Sum() / MeasurementIterations;
            var msgPerSec = (_eventsCount / avgTime) * 1000;

            Output.WriteLine($"Average time: {avgTime} ms, {msgPerSec} msg/sec");
        }

        internal void MeasureGroup(Func<TimeSpan, string> msg, Action block, int numMsg,int numGroup)
        {
            var measurements = new List<TimeSpan>(MeasurementIterations);

            block();
            block(); //warm-up

            var i = 0;
            while (i < MeasurementIterations)
            {
                var sw = Stopwatch.StartNew();
                block();
                sw.Stop();
                measurements.Add(sw.Elapsed);
                Output.WriteLine(msg(sw.Elapsed));
                i++;
            }

            var avgTime = measurements.Select(c => c.TotalMilliseconds).Sum() / MeasurementIterations;
            var msgPerSec = (numMsg / avgTime) * 1000;
            var msgPerSecTotal = (numMsg*numGroup / avgTime) * 1000;
            Output.WriteLine($"Workers: {numGroup} , Average time: {avgTime} ms, {msgPerSec} msg/sec/actor, {msgPerSecTotal} total msg/sec.");
        }

        [DotMemoryUnit(CollectAllocations=true, FailIfRunWithoutSupport = false)]
        [Fact]
        public void DotMemory_PersistenceActor_performance_must_measure_Persist()
        {
            dotMemory.Check();

            var p1 = BenchActor("DotMemoryPersistPid", _eventsCount);

            dotMemory.Check((mem) =>
                {
                    Measure(
                        d => $"Persist()-ing {_eventsCount} took {d.TotalMilliseconds} ms",
                        () =>
                        {
                            FeedAndExpectLast(p1, "p", Commands);
                            p1.Tell(ResetCounter.Instance);
                        });
                }
            );
            dotMemory.Check((mem) =>
                {
                    Measure(
                        d => $"Persist()-ing {_eventsCount} took {d.TotalMilliseconds} ms",
                        () =>
                        {
                            FeedAndExpectLast(p1, "p", Commands);
                            p1.Tell(ResetCounter.Instance);
                        });
                }
            );
            dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }

        [DotMemoryUnit(CollectAllocations=true, FailIfRunWithoutSupport = false)]
        [Fact]
        public void DotMemory_PersistenceActor_performance_must_measure_PersistGroup400()
        {
            dotMemory.Check();

            const int numGroup = 400;
            var numCommands = Math.Min(_eventsCount/100,500);

            dotMemory.Check((mem) =>
                {
                    RunGroupBenchmark(numGroup, numCommands);
                }
            );
            dotMemory.Check((mem) =>
                {
                    RunGroupBenchmark(numGroup, numCommands);
                }
            );
            dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }


        [Fact]
        public void PersistenceActor_performance_must_measure_Persist()
        {

            var p1 = BenchActor("PersistPid", _eventsCount);

            //dotMemory.Check((mem) =>
            //{
                Measure(
                    d =>
                        $"Persist()-ing {_eventsCount} took {d.TotalMilliseconds} ms",
                    () =>
                    {
                        FeedAndExpectLast(p1, "p", Commands);
                        p1.Tell(ResetCounter.Instance);
                    });
            //}
            //);
            //dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }

        //[DotMemoryUnit(CollectAllocations=true, FailIfRunWithoutSupport = false)]
        /*
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
        public void PersistenceActor_performance_must_measure_PersistGroup10()
        {
            const int numGroup = 10;
            var numCommands = Math.Min(_eventsCount/10,1000);
            RunGroupBenchmark(numGroup, numCommands);
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_PersistGroup25()
        {
            const int numGroup = 25;
            var numCommands = Math.Min(_eventsCount/25,1000);
            RunGroupBenchmark(numGroup, numCommands);
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_PersistGroup50()
        {
            const int numGroup = 50;
            var numCommands = Math.Min(_eventsCount/50,1000);
            RunGroupBenchmark(numGroup, numCommands);
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_PersistGroup100()
        {
            const int numGroup = 100;
            var numCommands = Math.Min(_eventsCount/100,1000);
            RunGroupBenchmark(numGroup, numCommands);
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_PersistGroup200()
        {
            const int numGroup = 200;
            var numCommands = Math.Min(_eventsCount/100,500);
            RunGroupBenchmark(numGroup, numCommands);
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_PersistGroup400()
        {
            const int numGroup = 400;
            var numCommands = Math.Min(_eventsCount/100,500);
            RunGroupBenchmark(numGroup, numCommands);
        }

        protected void RunGroupBenchmark(int numGroup, int numCommands)
        {
            var p1 = BenchActorNewProbeGroup("GroupPersistPid" + numGroup, numGroup, numCommands);
            MeasureGroup(
                d => $"Persist()-ing {numCommands} * {numGroup} took {d.TotalMilliseconds} ms",
                () =>
                {
                    FeedAndExpectLastRouterSet(p1, "p",
                        Commands.Take(numCommands).ToImmutableList(),
                        numGroup);
                    p1.aut.Tell(new Broadcast(ResetCounter.Instance));
                }, numCommands, numGroup
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
        public void PersistenceActor_performance_must_measure_PersistAll()
        {
            var p1 = BenchActor("PersistAllPid", _eventsCount);
            Measure(d => $"PersistAll()-ing {_eventsCount} took {d.TotalMilliseconds} ms", () =>
            {
                FeedAndExpectLast(p1, "pb", Commands);
                p1.Tell(ResetCounter.Instance);
            });
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_PersistAsync()
        {
            var p1 = BenchActor("PersistAsyncPid", _eventsCount);
            Measure(d => $"PersistAsync()-ing {_eventsCount} took {d.TotalMilliseconds} ms", () =>
            {
                FeedAndExpectLast(p1, "pa", Commands);
                p1.Tell(ResetCounter.Instance);
            });
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_PersistAllAsync()
        {
            var p1 = BenchActor("PersistAllAsyncPid", _eventsCount);
            Measure(d => $"PersistAllAsync()-ing {_eventsCount} took {d.TotalMilliseconds} ms", () =>
            {
                FeedAndExpectLast(p1, "pba", Commands);
                p1.Tell(ResetCounter.Instance);
            });
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_Recovering()
        {
            var p1 = BenchActor("PersistRecoverPid", _eventsCount);

            FeedAndExpectLast(p1, "p", Commands);
            Measure(d => $"Recovering {_eventsCount} took {d.TotalMilliseconds} ms", () =>
            {
                BenchActor("PersistRecoverPid", _eventsCount);
                _testProbe.ExpectMsg(Commands.Last(), _expectDuration);
            });
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_RecoveringTwo()
        {
            var p1 = BenchActorNewProbe("DoublePersistRecoverPid1", _eventsCount);
            var p2 = BenchActorNewProbe("DoublePersistRecoverPid2", _eventsCount);
            FeedAndExpectLastSpecific(p1, "p", Commands);
            FeedAndExpectLastSpecific(p2, "p", Commands);
            MeasureGroup(d => $"Recovering {_eventsCount} took {d.TotalMilliseconds} ms", () =>
            {
               var task1 = Task.Run(()=>
               {
                   var refAndProbe =BenchActorNewProbe("DoublePersistRecoverPid1",
                           _eventsCount);
                   refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
               });
               var task2 =Task.Run(() =>
               {
                   var refAndProbe =BenchActorNewProbe("DoublePersistRecoverPid2", _eventsCount);
                   refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
               });
               Task.WaitAll(new[] {task1, task2});

            },_eventsCount,2);
        }
        [Fact]
        public void PersistenceActor_performance_must_measure_RecoveringFour()
        {
            var p1 = BenchActorNewProbe("QuadPersistRecoverPid1", _eventsCount);
            var p2 = BenchActorNewProbe("QuadPersistRecoverPid2", _eventsCount);
            var p3 = BenchActorNewProbe("QuadPersistRecoverPid3", _eventsCount);
            var p4 = BenchActorNewProbe("QuadPersistRecoverPid4", _eventsCount);
            FeedAndExpectLastSpecific(p1, "p", Commands);
            FeedAndExpectLastSpecific(p2, "p", Commands);
            FeedAndExpectLastSpecific(p3, "p", Commands);
            FeedAndExpectLastSpecific(p4, "p", Commands);
            MeasureGroup(d => $"Recovering {_eventsCount} took {d.TotalMilliseconds} ms", () =>
            {
                var task1 = Task.Run(()=>
                {
                    var refAndProbe =BenchActorNewProbe("QuadPersistRecoverPid1",
                        _eventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
                });
                var task2 =Task.Run(() =>
                {
                    var refAndProbe =BenchActorNewProbe("QuadPersistRecoverPid2", _eventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
                });
                var task3 =Task.Run(() =>
                {
                    var refAndProbe =BenchActorNewProbe("QuadPersistRecoverPid3", _eventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
                });
                var task4 =Task.Run(() =>
                {
                    var refAndProbe =BenchActorNewProbe("QuadPersistRecoverPid4", _eventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
                });
                Task.WaitAll(new[] {task1, task2,task3,task4});

            },_eventsCount,4);
        }
        [Fact]
        public void PersistenceActor_performance_must_measure_Recovering8()
        {
            var p1 = BenchActorNewProbe("OctPersistRecoverPid1", _eventsCount);
            var p2 = BenchActorNewProbe("OctPersistRecoverPid2", _eventsCount);
            var p3 = BenchActorNewProbe("OctPersistRecoverPid3", _eventsCount);
            var p4 = BenchActorNewProbe("OctPersistRecoverPid4", _eventsCount);
            var p5 = BenchActorNewProbe("OctPersistRecoverPid5", _eventsCount);
            var p6 = BenchActorNewProbe("OctPersistRecoverPid6", _eventsCount);
            var p7 = BenchActorNewProbe("OctPersistRecoverPid7", _eventsCount);
            var p8 = BenchActorNewProbe("OctPersistRecoverPid8", _eventsCount);
            FeedAndExpectLastSpecific(p1, "p", Commands);
            FeedAndExpectLastSpecific(p2, "p", Commands);
            FeedAndExpectLastSpecific(p3, "p", Commands);
            FeedAndExpectLastSpecific(p4, "p", Commands);
            FeedAndExpectLastSpecific(p5, "p", Commands);
            FeedAndExpectLastSpecific(p6, "p", Commands);
            FeedAndExpectLastSpecific(p7, "p", Commands);
            FeedAndExpectLastSpecific(p8, "p", Commands);
            MeasureGroup(d => $"Recovering {_eventsCount} took {d.TotalMilliseconds} ms , {(_eventsCount*8 / d.TotalMilliseconds) * 1000} total msg/sec", () =>
            {
                var task1 = Task.Run(()=>
                {
                    var refAndProbe =BenchActorNewProbe("OctPersistRecoverPid1",
                        _eventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
                });
                var task2 =Task.Run(() =>
                {
                    var refAndProbe =BenchActorNewProbe("OctPersistRecoverPid2", _eventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
                });
                var task3 =Task.Run(() =>
                {
                    var refAndProbe =BenchActorNewProbe("OctPersistRecoverPid3", _eventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
                });
                var task4 =Task.Run(() =>
                {
                    var refAndProbe =BenchActorNewProbe("OctPersistRecoverPid4", _eventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
                });
                var task5 =Task.Run(() =>
                {
                    var refAndProbe =BenchActorNewProbe("OctPersistRecoverPid5", _eventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
                });
                var task6 =Task.Run(() =>
                {
                    var refAndProbe =BenchActorNewProbe("OctPersistRecoverPid6", _eventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
                });
                var task7 =Task.Run(() =>
                {
                    var refAndProbe =BenchActorNewProbe("OctPersistRecoverPid7", _eventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
                });
                var task8 =Task.Run(() =>
                {
                    var refAndProbe =BenchActorNewProbe("OctPersistRecoverPid8", _eventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), _expectDuration);
                });
                Task.WaitAll(new[] {task1, task2,task3,task4,task5,task6,task7,task8});

            },_eventsCount,8);
        }
    }

    internal class ResetCounter
    {
        public static ResetCounter Instance { get; } = new ResetCounter();
        private ResetCounter() { }
    }

    public class Cmd
    {
        public Cmd(string mode, int payload)
        {
            Mode = mode;
            Payload = payload;
        }

        public string Mode { get; }

        public int Payload { get; }
    }

    internal class BenchActor : UntypedPersistentActor
    {
        private int _counter = 0;
        private const int BatchSize = 50;
        private List<Cmd> _batch = new List<Cmd>(BatchSize);

        public BenchActor(string persistenceId, IActorRef replyTo, int replyAfter, bool groupName)
        {
            PersistenceId = persistenceId + MurmurHash.StringHash(Context.Parent.Path.Name + Context.Self.Path.Name);
            ReplyTo = replyTo;
            ReplyAfter = replyAfter;
        }
        public BenchActor(string persistenceId, IActorRef replyTo, int replyAfter)
        {
            PersistenceId = persistenceId;
            ReplyTo = replyTo;
            ReplyAfter = replyAfter;
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
                    if (c.Payload != _counter) throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{c.Payload}]");
                    if (_counter == ReplyAfter) ReplyTo.Tell(c.Payload);
                    break;
            }
        }

        protected override void OnCommand(object message)
        {
            switch (message)
            {
                case Cmd c when c.Mode == "p":
                    Persist(c, d =>
                    {
                        _counter += 1;
                        if (d.Payload != _counter) throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{d.Payload}]");
                        if (_counter == ReplyAfter) ReplyTo.Tell(d.Payload);
                    });
                    break;
                case Cmd c when c.Mode == "pb":
                    _batch.Add(c);

                    if (_batch.Count % BatchSize == 0)
                    {

                        PersistAll(_batch, d =>
                        {
                            _counter += 1;
                            if (d.Payload != _counter) throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{d.Payload}]");
                            if (_counter == ReplyAfter) ReplyTo.Tell(d.Payload);
                        });
                        _batch = new List<Cmd>(BatchSize);
                    }
                    break;
                case Cmd c when c.Mode == "pa":
                    PersistAsync(c, d =>
                    {
                        _counter += 1;
                        if (d.Payload != _counter) throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{d.Payload}]");
                        if (_counter == ReplyAfter) ReplyTo.Tell(d.Payload);
                    });
                    break;
                case Cmd c when c.Mode == "pba":
                    _batch.Add(c);

                    if (_batch.Count % BatchSize == 0)
                    {
                        PersistAllAsync(_batch, d =>
                        {
                            _counter += 1;
                            if (d.Payload != _counter) throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{d.Payload}]");
                            if (_counter == ReplyAfter) ReplyTo.Tell(d.Payload);
                        });
                        _batch = new List<Cmd>(BatchSize);
                    }
                    break;
                case ResetCounter _:
                    _counter = 0;
                    break;
            }
        }
    }
}
