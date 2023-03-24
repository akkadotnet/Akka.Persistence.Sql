// -----------------------------------------------------------------------
//  <copyright file="BaseCurrentEventsByPersistenceIdSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.TCK.Query;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Akka.Persistence.Sql.Tests.Common.Query
{
    public abstract class BaseCurrentEventsByPersistenceIdSpec : CurrentEventsByPersistenceIdSpec, IAsyncLifetime
    {
        private readonly ITestConfig _config;
        private readonly TestFixture _fixture;

        protected BaseCurrentEventsByPersistenceIdSpec(ITestConfig config, ITestOutputHelper output, TestFixture fixture)
            : base(Config(config, fixture), nameof(CurrentEventsByPersistenceIdSpec), output)
        {
            _config = config;
            _fixture = fixture;
            var persistence = Persistence.Instance.Apply(Sys);
            persistence.JournalFor(null);
        }

        public Task InitializeAsync()
        {
            ReadJournal = Sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            using var cts = new CancellationTokenSource(10.Seconds());
            await Task.WhenAny(
                Task.Delay(Timeout.Infinite, cts.Token),
                _fixture.InitializeDbAsync(_config.Database));
            
            if(cts.IsCancellationRequested)
                throw new XunitException("Failed to clean up database in 10 seconds");
        }

        private static Configuration.Config Config(ITestConfig config, TestFixture fixture)
            => ConfigurationFactory.ParseString($@"
                    akka.loglevel = INFO
                    akka.persistence {{
                        journal {{
                            plugin = ""akka.persistence.journal.sql""
                            sql {{
                                provider-name = ""{config.Provider}""
                                tag-write-mode = ""{config.TagMode}""
                                connection-string = ""{fixture.ConnectionString(config.Database)}""
                                auto-initialize = on
                            }}
                        }}
                        query {{
                            journal {{
                                sql {{
                                    provider-name = ""{config.Provider}""
                                    connection-string = ""{fixture.ConnectionString(config.Database)}""
                                    tag-read-mode = ""{config.TagMode}""
                                    auto-initialize = on
                                    refresh-interval = 1s
                                }}
                            }}
                        }}
                    }}
                    akka.test.single-expect-default = 10s")
                .WithFallback(SqlPersistence.DefaultConfiguration);
    }
}
