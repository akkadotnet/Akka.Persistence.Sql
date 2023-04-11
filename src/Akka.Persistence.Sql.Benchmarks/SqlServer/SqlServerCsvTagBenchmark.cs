﻿// -----------------------------------------------------------------------
//  <copyright file="SqlServerCsvTagBenchmark.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Benchmark.Common;
using Akka.Persistence.Sql.Benchmarks.Configurations;
using Akka.Persistence.Sql.Query;
using Akka.Streams;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;

namespace Akka.Persistence.Sql.Benchmarks.SqlServer
{
    //[Config(typeof(MicroBenchmarkConfig))]
    [SimpleJob(RunStrategy.ColdStart, iterationCount:1, warmupCount:0)]
    public class SqlServerCsvTagBenchmark
    {
        private const string ConnectionString = "Server=localhost,9908;User Id=sa;Password=Password12!;TrustServerCertificate=true;Database=sql_tests_3d8f4ff9fff543cb8cbb0cce57b81ef6";
        private const string ProviderName = "SqlServer.2019";
        private const string TagMode = "TagTable";
        
        private ActorSystem? _sys;
        private IReadJournal? _readJournal;
        private IMaterializer? _materializer;

        [GlobalSetup]
        public async Task Setup()
        {
            var config = ConfigurationFactory.ParseString(@$"
akka.persistence.journal {{
    plugin = akka.persistence.journal.sql
    sql {{
        connection-string = ""{ConnectionString}""
        provider-name = ""{ProviderName}""
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
	connection-string = ""{ConnectionString}""
	provider-name = {ProviderName}
    tag-read-mode = {TagMode}
    # journal-sequence-retrieval.query-delay = 50ms
}}")
                .WithFallback(Persistence.DefaultConfig())
                .WithFallback(SqlPersistence.DefaultConfiguration);
            
            _sys = ActorSystem.Create("system", config);
            _materializer = _sys.Materializer();
            _readJournal = _sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
            
            DebuggingHelpers.TraceDumpOn(_sys.Log);
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
            var count = 0;
            var source = ((ICurrentEventsByTagQuery)_readJournal!).CurrentEventsByTag("TAG", NoOffset.Instance);
            await source.RunForeach(_ =>
            {
                count++;
            }, _materializer);
            if (count != 10)
                throw new Exception($"Assertion failed, expected count: 10, received: {count}");
        }
    }
}
