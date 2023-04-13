// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common.Containers;
using BenchmarkDotNet.Running;
using LanguageExt.UnitsOfMeasure;

namespace Akka.Persistence.Sql.Benchmarks;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if(args.Length == 0)
        {
            BenchmarkSwitcher.FromAssembly(Assembly.GetExecutingAssembly()).Run(args);
            return;
        }

        if (args[0].ToLowerInvariant() == "generate")
        {
            var fixture = new SqlServerContainer();
            await fixture.InitializeAsync();
            
            var config = ConfigurationFactory.ParseString(@$"
akka.persistence.journal {{
    plugin = akka.persistence.journal.sql
    sql {{
        connection-string = ""{fixture.ConnectionString}""
        provider-name = ""{fixture.ProviderName}""
        tag-write-mode = Both
        event-adapters {{
            event-tagger = ""{typeof(EventTagger).AssemblyQualifiedName}""
        }}
        event-adapter-bindings {{
            ""System.Int32"" = event-tagger
        }}
    }}
}}")
                .WithFallback(Persistence.DefaultConfig())
                .WithFallback(SqlPersistence.DefaultConfiguration);
            
            var sys = ActorSystem.Create("Initializer", config);
            var initializer = sys.ActorOf(Props.Create(() => new InitializeDbActor()), "INITIALIZER");
            await initializer.Ask<InitializeDbActor.Initialized>(
                InitializeDbActor.Initialize.Instance,
                20.Minutes());
            await sys.Terminate();
            
            await File.WriteAllTextAsync("benchmark.conf", $@"
benchmark {{
    connection-string = ""{fixture.ConnectionString}""
    provider-name = ""{fixture.ProviderName}""
}}");
            
            Console.WriteLine($"Connection String: {fixture.ConnectionString}");
            Console.WriteLine($"Provider Name: {fixture.ProviderName}");
        }
    }
}
