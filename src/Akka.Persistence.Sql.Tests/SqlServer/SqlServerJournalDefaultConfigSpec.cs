// -----------------------------------------------------------------------
//  <copyright file="SqlServerJournalDefaultConfigSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.TCK.Journal;
using Akka.TestKit.Extensions;
using FluentAssertions;
using FluentAssertions.Extensions;
using LinqToDB.SchemaProvider;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.SqlServer
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(SqlServerPersistenceSpec))]
    public class SqlServerJournalDefaultConfigSpec : JournalSpec
    {
        // journal table column names
        private readonly string[] _journalTableColumnNames =
        {
            "ordering",
            "deleted",
            "persistence_id",
            "sequence_number",
            "created",
            "message",
            "identifier",
            "manifest",
            "writer_uuid",
        };

        // metadata table column names
        private readonly string[] _metadataTableColumnNames =
        {
            "persistence_id",
            "sequence_number",
        };

        // tag table column names
        private readonly string[] _tagTableColumnNames =
        {
            "ordering_id",
            "tag",
            "persistence_id",
            "sequence_nr",
        };

        public SqlServerJournalDefaultConfigSpec(ITestOutputHelper output, SqlServerContainer fixture)
            : base(Configuration(fixture), nameof(SqlServerJournalDefaultConfigSpec), output)
        {
            Initialize();
        }

        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;

        private static Configuration.Config Configuration(SqlServerContainer fixture)
        {
            if (!fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");

            return SqlJournalDefaultSpecConfig.GetDefaultConfig(fixture.ProviderName, fixture.ConnectionString);
        }

        [Fact(DisplayName = "Database created using default configuration should contain proper tables and columns")]
        public async Task DefaultTableTest()
        {
            // Initialize journal
            var journal = Persistence.Instance.Apply(Sys).JournalFor(null);

            // wait until journal is initialized
            var _ = await journal.Ask<ActorIdentity>(new Identify(null)).ShouldCompleteWithin(3.Seconds());

            var config = GetConfig();
            var schema = GetSchema(config);

            // journal table
            var journalTable = schema.Tables.FirstOrDefault(t => t.TableName == "journal");
            journalTable.Should().NotBeNull();
            var journalColumns = journalTable!.Columns.Select(c => c.ColumnName).ToList();
            foreach (var column in _journalTableColumnNames)
            {
                if (!journalColumns.Remove(column))
                    throw new XunitException($"Journal table does not contain the required column {column}");
            }

            journalColumns.Should().BeEmpty("Journal table should not contain any superfluous columns");

            // tag table
            var tagTable = schema.Tables.FirstOrDefault(t => t.TableName == "tags");
            tagTable.Should().NotBeNull();
            var tagColumns = tagTable!.Columns.Select(c => c.ColumnName).ToList();
            foreach (var column in _tagTableColumnNames)
            {
                if (!tagColumns.Remove(column))
                    throw new XunitException($"Tag table does not contain the required column {column}");
            }

            tagColumns.Should().BeEmpty("Tag table should not contain any superfluous columns");
        }

        // Used to test that metadata table is valid
        private void AssertMetadataTableExistsAndValid(DatabaseSchema schema)
        {
            var metadataTable = schema.Tables.FirstOrDefault(t => t.TableName == "journal_metadata");
            metadataTable.Should().NotBeNull();
            var metadataColumns = metadataTable!.Columns.Select(c => c.ColumnName).ToList();
            foreach (var column in _metadataTableColumnNames)
            {
                if (!metadataColumns.Remove(column))
                    throw new XunitException($"Journal metadata table does not contain the required column {column}");
            }

            metadataColumns.Should().BeEmpty("Journal metadata table should not contain any superfluous columns");
        }

        private JournalConfig GetConfig()
        {
            var config = Sys.Settings.Config.GetConfig("akka.persistence.journal.sql")
                .WithFallback(SqlPersistence.DefaultJournalConfiguration);
            return new JournalConfig(config);
        }

        private static DatabaseSchema GetSchema(JournalConfig journalConfig)
        {
            var connectionFactory = new AkkaPersistenceDataConnectionFactory(journalConfig);
            var connection = connectionFactory.GetConnection();
            return connection.GetSchema();
        }
    }
}
