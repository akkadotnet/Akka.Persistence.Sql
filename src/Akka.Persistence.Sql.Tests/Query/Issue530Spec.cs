// -----------------------------------------------------------------------
//  <copyright file="Issue530Spec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Hosting;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Sqlite;
using Akka.Persistence.TCK;
using FluentAssertions;
using LanguageExt.UnitsOfMeasure;
using Xunit;
using Xunit.Abstractions;
using static FluentAssertions.FluentActions;

namespace Akka.Persistence.Sql.Tests;

[Collection(nameof(SqlitePersistenceSpec))]
public class Issue530Spec: PluginSpec
{
    public Issue530Spec(ITestOutputHelper output, SqliteContainer fixture)
        : base(FromConfig(Config(fixture)), nameof(Issue530Spec), output)
    {
    }
    
    [Fact]
    public void MultipleReadJournalsTest()
    {
        var query = PersistenceQuery.Get(Sys);
        
        Invoking(() =>
            {
                var readJournal1 = query.ReadJournalFor<SqlReadJournal>("akka.persistence.query.journal.sql");
                var readJournal2 = query.ReadJournalFor<SqlReadJournal>("akka.persistence.query.journal.sql2");
                var readJournal3 = query.ReadJournalFor<SqlReadJournal>("akka.persistence.query.journal.sql3");
            })
            .Should().NotThrow<InvalidActorNameException>();
    }

    private static Configuration.Config Config(SqliteContainer fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            var baseConfig = ConfigurationFactory.ParseString(
                    $$"""
                      akka {
                          loglevel = INFO
                          persistence {
                              journal {
                                  plugin = "akka.persistence.journal.sql"
                                  auto-start-journals = [ "akka.persistence.journal.sql" ]
                                  sql {
                                      provider-name = "{{fixture.ProviderName}}"
                                      connection-string = "{{fixture.ConnectionString}}"
                                  }
                              }
                              query.journal.sql {
                                  provider-name = "{{fixture.ProviderName}}"
                                  connection-string = "{{fixture.ConnectionString}}"
                                  refresh-interval = 1s
                                  max-buffer-size = 3
                              }
                          }
                      }
                      akka.test.single-expect-default = 10s
                      """)
                .WithFallback(SqlPersistence.DefaultConfiguration);
            
            // new read journal pointing to a different write plugin
            var config1 = ConfigurationFactory.ParseString(
                    """
                    akka.persistence.query.journal.sql2 {
                        plugin-id = akka.persistence.query.journal.sql2
                        write-plugin = akka.persistence.journal.sql2
                    }
                    """)
                .WithFallback(
                    baseConfig.GetConfig("akka.persistence.journal.sql")
                        .MoveTo("akka.persistence.journal.sql2"))
                .WithFallback(
                    baseConfig.GetConfig("akka.persistence.query.journal.sql")
                        .MoveTo("akka.persistence.query.journal.sql2"));
            
            // new read journal pointing to the default write plugin
            return ConfigurationFactory.ParseString("akka.persistence.query.journal.sql3.plugin-id = akka.persistence.query.journal.sql3")
                .WithFallback(config1)
                .WithFallback(
                    baseConfig.GetConfig("akka.persistence.query.journal.sql")
                    .MoveTo("akka.persistence.query.journal.sql3"))
                .WithFallback(baseConfig);
        }

}
