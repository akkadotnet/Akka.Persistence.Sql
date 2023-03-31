// -----------------------------------------------------------------------
//  <copyright file="BasePersistenceIdsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Utility;
using Akka.Persistence.TCK.Query;
using Akka.TestKit.Extensions;
using FluentAssertions.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Common.Query
{
    public abstract class BasePersistenceIdsSpec<T> : PersistenceIdsSpec, IAsyncLifetime where T : ITestContainer
    {
        protected BasePersistenceIdsSpec(TagMode tagMode, ITestOutputHelper output, string name, T fixture)
            : base(Config(tagMode, fixture), name, output)
        {
            ReadJournal = Sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
        }

        public async Task InitializeAsync()
        {
            // Force start read journal
            var journal = Persistence.Instance.Apply(Sys).JournalFor(null);
            
            // Wait until journal is ready
            var _ = await journal.Ask<Initialized>(IsInitialized.Instance).ShouldCompleteWithin(3.Seconds());
        }

        public Task DisposeAsync()
            => Task.CompletedTask;

        private static Configuration.Config Config(TagMode tagMode, T fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
            
            return ConfigurationFactory.ParseString($@"
akka.loglevel = INFO
akka.actor{{
    serializers{{
        persistence-tck-test=""Akka.Persistence.TCK.Serialization.TestSerializer,Akka.Persistence.TCK""
    }}
    serialization-bindings {{
        ""Akka.Persistence.TCK.Serialization.TestPayload,Akka.Persistence.TCK"" = persistence-tck-test
    }}
}}
akka.persistence {{
    publish-plugin-commands = on
    journal {{
        plugin = ""akka.persistence.journal.sql""
        auto-start-journals = [ ""akka.persistence.journal.sql"" ]
        sql {{
            provider-name = ""{fixture.ProviderName}""
            tag-write-mode = ""{tagMode}""
            connection-string = ""{fixture.ConnectionString}""
            auto-initialize = on
        }}
    }}
    snapshot-store {{
        plugin = ""akka.persistence.snapshot-store.sql""
        sql {{
            provider-name = ""{fixture.ProviderName}""
            connection-string = ""{fixture.ConnectionString}""
            auto-initialize = on
        }}
    }}
}}
akka.persistence.query.journal.sql {{
    provider-name = ""{fixture.ProviderName}""
    connection-string = ""{fixture.ConnectionString}""
    tag-read-mode = ""{tagMode}""
    auto-initialize = on
    refresh-interval = 200ms
}}
akka.test.single-expect-default = 10s")
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }
    }
}
