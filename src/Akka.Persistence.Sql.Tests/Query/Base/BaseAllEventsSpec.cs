// -----------------------------------------------------------------------
//  <copyright file="SqliteAllEventsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.TCK.Query;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.Base
{
    public abstract class BaseAllEventsSpec : AllEventsSpec, IAsyncLifetime
    {
        private readonly ITestConfig _config;
        private readonly TestFixture _fixture;

        protected BaseAllEventsSpec(ITestConfig config, ITestOutputHelper output, TestFixture fixture)
            : base(Config(config, fixture), nameof(AllEventsSpec), output)
        {
            _config = config;
            _fixture = fixture;
        }

        public async Task InitializeAsync()
        {
            await _fixture.InitializeDbAsync(_config.Database);
            ReadJournal = Sys.ReadJournalFor<Linq2DbReadJournal>(Linq2DbReadJournal.Identifier);
        }

        public Task DisposeAsync()
            => Task.CompletedTask;

        private static Configuration.Config Config(ITestConfig config, TestFixture fixture)
            => ConfigurationFactory.ParseString($@"
                    akka.loglevel = INFO
                    akka.persistence.journal.plugin = ""akka.persistence.journal.linq2db""
                    akka.persistence.journal.linq2db {{
                        event-adapters {{
                            color-tagger  = ""Akka.Persistence.TCK.Query.ColorFruitTagger, Akka.Persistence.TCK""
                        }}
                        event-adapter-bindings = {{
                            ""System.String"" = color-tagger
                        }}
                        provider-name = ""{config.Provider}""
                        tag-write-mode = ""{config.TagWriteMode}""
                        table-mapping = ""{config.TableMapping}""
                        connection-string = ""{fixture.ConnectionString(config.Database)}""
                        auto-initialize = on
                        refresh-interval = 1s
                    }}
                    akka.persistence.query.journal.linq2db {{
                        provider-name = ""{config.Provider}""
                        connection-string = ""{fixture.ConnectionString(config.Database)}""
                        tag-read-mode = ""{config.TagReadMode}""
                        table-mapping = ""{config.TableMapping}""
                        auto-initialize = on
                    }}
                    akka.test.single-expect-default = 10s")
                .WithFallback(Linq2DbPersistence.DefaultConfiguration);
    }
}
