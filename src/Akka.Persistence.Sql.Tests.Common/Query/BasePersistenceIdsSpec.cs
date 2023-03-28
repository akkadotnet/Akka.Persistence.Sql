// -----------------------------------------------------------------------
//  <copyright file="BasePersistenceIdsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Query;
using FluentAssertions.Extensions;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Common.Query
{
    public abstract class BasePersistenceIdsSpec<T> : PersistenceIdsSpec where T : ITestContainer
    {
        protected BasePersistenceIdsSpec(TagMode tagMode, ITestOutputHelper output, string name, T fixture)
            : base(Config(tagMode, fixture), name, output)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
            ReadJournal = Sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
        }

        private static Configuration.Config Config(TagMode tagMode, T fixture)
            => ConfigurationFactory.ParseString($@"
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
