// -----------------------------------------------------------------------
//  <copyright file="SqlitePersistenceIdsSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Tests.Common;
using Akka.Persistence.Sqlite;
using Akka.Persistence.TCK.Query;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Tests.Query.Base
{
    public abstract class BasePersistenceIdsSpec : PersistenceIdsSpec, IAsyncLifetime
    {
        private readonly ITestConfig _config;
        private readonly TestFixture _fixture;

        public BasePersistenceIdsSpec(ITestConfig config, ITestOutputHelper output, TestFixture fixture)
            : base(Config(config, fixture), nameof(PersistenceIdsSpec), output)
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
                            plugin = ""akka.persistence.journal.linq2db""
                            linq2db = {{
                                provider-name = ""{config.Provider}""
                                tag-write-mode = ""{config.TagWriteMode}""
                                table-mapping = ""{config.TableMapping}""
                                connection-string = ""{fixture.ConnectionString(config.Database)}""
                                auto-initialize = on
                                refresh-interval = 200ms
                            }}
                        }}
                        snapshot-store {{
                            plugin = ""akka.persistence.snapshot-store.linq2db""
                            linq2db {{
                                provider-name = ""{config.Provider}""
                                table-mapping = ""{config.TableMapping}""
                                connection-string = ""{fixture.ConnectionString(config.Database)}""
                                auto-initialize = on
                            }}
                        }}
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
