// -----------------------------------------------------------------------
//  <copyright file="BaseCurrentAllEventsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
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

namespace Akka.Persistence.Sql.Tests.Common.Query
{
    public abstract class BaseCurrentAllEventsSpec<T> : CurrentAllEventsSpec where T : ITestContainer
    {
        protected BaseCurrentAllEventsSpec(TagMode tagMode, ITestOutputHelper output, string name, T fixture)
            : base(Config(tagMode, fixture), name, output)
        {
            ReadJournal = Sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);

            // Force start read journal and wait until it is ready
            var journal = Persistence.Instance.Apply(Sys).JournalFor(null);
            journal.Ask<Initialized>(IsInitialized.Instance).Wait(3.Seconds());
        }

        private static Configuration.Config Config(TagMode tagMode, T fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            return ConfigurationFactory.ParseString(
                    $@"
akka.loglevel = INFO
akka.persistence.journal.plugin = ""akka.persistence.journal.sql""
akka.persistence.journal.auto-start-journals = [ ""akka.persistence.journal.sql"" ]
akka.persistence.journal.sql {{
    event-adapters {{
        color-tagger  = ""Akka.Persistence.TCK.Query.ColorFruitTagger, Akka.Persistence.TCK""
    }}
    event-adapter-bindings = {{
        ""System.String"" = color-tagger
    }}
    provider-name = ""{fixture.ProviderName}""
    tag-write-mode = ""{tagMode}""
    connection-string = ""{fixture.ConnectionString}""
}}
akka.persistence.query.journal.sql {{
    provider-name = ""{fixture.ProviderName}""
    connection-string = ""{fixture.ConnectionString}""
    tag-read-mode = ""{tagMode}""
    refresh-interval = 1s
}}
akka.test.single-expect-default = 10s")
                .WithFallback(SqlPersistence.DefaultConfiguration);
        }
    }
}
