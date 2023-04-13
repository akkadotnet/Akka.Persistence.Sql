// -----------------------------------------------------------------------
//  <copyright file="SqlServerCsvTagBenchmark.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Benchmarks.Configurations;
using Akka.Persistence.Sql.Query;
using Akka.Streams;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using FluentAssertions;

namespace Akka.Persistence.Sql.Benchmarks.SqlServer
{
    [Config(typeof(MicroBenchmarkConfig))]
    //[SimpleJob(RunStrategy.ColdStart, iterationCount:1, warmupCount:0)]
    public class SqlServerCsvTagBenchmark
    {
        [Params("TagTable", "Csv")] 
        public string? TagMode { get; set; }
        
        private ActorSystem? _sys;
        private IReadJournal? _readJournal;
        private IMaterializer? _materializer;

        [GlobalSetup]
        public async Task Setup()
        {
            var benchConfig = ConfigurationFactory.ParseString(await File.ReadAllTextAsync("benchmark.conf"));
            var connectionString = benchConfig.GetString("benchmark.connection-string");
            var providerName = benchConfig.GetString("benchmark.provider-name");
            
            var config = ConfigurationFactory.ParseString(@$"
akka.persistence.journal {{
    plugin = akka.persistence.journal.sql
    sql {{
        connection-string = ""{connectionString}""
        provider-name = ""{providerName}""
        tag-write-mode = {TagMode}
        event-adapters {{
            event-tagger = ""{typeof(EventTagger).AssemblyQualifiedName}""
        }}
        event-adapter-bindings {{
            ""System.Int32"" = event-tagger
        }}
    }}
}}
akka.persistence.query.journal.sql {{
	connection-string = ""{connectionString}""
	provider-name = {providerName}
    tag-read-mode = {TagMode}
    # journal-sequence-retrieval.query-delay = 50ms
}}")
                .WithFallback(Persistence.DefaultConfig())
                .WithFallback(SqlPersistence.DefaultConfiguration);
            
            _sys = ActorSystem.Create("system", config);
            _materializer = _sys.Materializer();
            _readJournal = _sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
            
            //DebuggingHelpers.TraceDumpOn(_sys.Log);
        }

        [GlobalCleanup]
        public async Task Cleanup()
        {
            if(_sys is { })
                await _sys.Terminate();
        }

        [Benchmark]
        public async Task QueryByTag()
        {
            var events = new List<EventEnvelope>();
            var source = ((ICurrentEventsByTagQuery)_readJournal!).CurrentEventsByTag("TAG", NoOffset.Instance);
            await source.RunForeach(msg =>
            {
                events.Add(msg);
            }, _materializer);
            events.Select(e => e.SequenceNr).Should().BeEquivalentTo(Enumerable.Range(2000001, 10));
        }
    }
}
