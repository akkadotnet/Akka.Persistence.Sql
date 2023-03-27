﻿// -----------------------------------------------------------------------
//  <copyright file="BaseCurrentEventsByPersistenceIdSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.TCK.Query;
using FluentAssertions.Extensions;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Common.Query
{
    public abstract class BaseCurrentEventsByPersistenceIdSpec : CurrentEventsByPersistenceIdSpec
    {
        private readonly ITestConfig _config;
        private readonly TestFixture _fixture;

        protected BaseCurrentEventsByPersistenceIdSpec(ITestConfig config, ITestOutputHelper output, TestFixture fixture)
            : base(Config(config, fixture), nameof(CurrentEventsByPersistenceIdSpec), output)
        {
            _config = config;
            _fixture = fixture;
            ReadJournal = Sys.ReadJournalFor<SqlReadJournal>(SqlReadJournal.Identifier);
        }

        protected override void AfterAll()
        {
            base.AfterAll();
            Shutdown();
            if (!_fixture.InitializeDbAsync(_config.Database).Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
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