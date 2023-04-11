using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Sql.Tests.Common.Containers;
using LanguageExt.UnitsOfMeasure;

namespace Akka.Persistence.Sql.Benchmark.Common
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var fixture = new SqlServerContainer();
            await fixture.InitializeAsync();
            
            var config = ConfigurationFactory.ParseString(@$"
akka.persistence.journal {{
    plugin = akka.persistence.journal.sql
    sql {{
        connection-string = ""{fixture.ConnectionString}""
        provider-name = ""{fixture.ProviderName}""
        tag-write-mode = {args[0]}
        event-adapters {{
            event-tagger = ""{typeof(EventTagger).AssemblyQualifiedName}""
        }}
        event-adapter-bindings {{
            ""System.Int32"" = event-tagger
        }}
    }}
}}
akka.persistence.query.journal.sql {{
	connection-string = ""{fixture.ConnectionString}""
	provider-name = {fixture.ProviderName}
    tag-read-mode = {args[0]}
}}")
                .WithFallback(Persistence.DefaultConfig())
                .WithFallback(SqlPersistence.DefaultConfiguration);
            
            var sys = ActorSystem.Create("Initializer", config);
            var initializer = sys.ActorOf(Props.Create(() => new InitializeDbActor()), "INITIALIZER");
            await initializer.Ask<InitializeDbActor.Initialized>(
                InitializeDbActor.Initialize.Instance,
                20.Minutes());
            await sys.Terminate();
            
            Console.WriteLine($"Connection String: {fixture.ConnectionString}");
            Console.WriteLine($"Provider Name: {fixture.ProviderName}");
            Console.ReadKey();
        }
    }
}


