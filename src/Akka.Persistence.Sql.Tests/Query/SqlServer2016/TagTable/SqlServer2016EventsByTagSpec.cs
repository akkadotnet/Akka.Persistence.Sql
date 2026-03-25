// -----------------------------------------------------------------------
//  <copyright file="SqlServer2016EventsByTagSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Config;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Query;
using Akka.Persistence.Sql.Tests.SqlServer;
using FluentAssertions;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;
using Xunit;
using Xunit.Abstractions;
#if !DEBUG
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
#endif

namespace Akka.Persistence.Sql.Tests.Query.SqlServer2016.TagTable
{
#if !DEBUG
    [SkipWindows]
#endif
    [Collection(nameof(SqlServer2016PersistenceSpec))]
    public class SqlServer2016EventsByTagSpec : BaseEventsByTagSpec<SqlServer2016Container>
    {
        private readonly SqlServer2016Container _fixture;

        public SqlServer2016EventsByTagSpec(ITestOutputHelper output, SqlServer2016Container fixture)
            : base(TagMode.TagTable, output, nameof(SqlServer2016EventsByTagSpec), fixture)
        {
            _fixture = fixture;
        }

        [Fact(DisplayName = "StringAggregate should throw on SQL Server 2016 compatibility level")]
        public async Task StringAggregateShouldThrowOnSqlServer2016()
        {
            // Arrange: connect to the database using the generic "SqlServer" provider
            // LinqToDB will auto-detect the compatibility level as 130 (SQL Server 2016)
            var dataProvider = SqlServerTools.GetDataProvider(
                SqlServerVersion.AutoDetect,
                SqlServerProvider.MicrosoftDataSqlClient,
                _fixture.ConnectionString);
            await using var db = new DataConnection(dataProvider, _fixture.ConnectionString);

            // Verify LinqToDB detected the version as SQL Server 2016
            var sqlServerProvider = db.DataProvider as SqlServerDataProvider;
            sqlServerProvider.Should().NotBeNull();
            sqlServerProvider!.Version.Should().Be(SqlServerVersion.v2016,
                "LinqToDB should auto-detect compatibility level 130 as SQL Server 2016");

            // Verify our SupportsStringAggregate property returns false
            var akkaConnection = new AkkaDataConnection(LinqToDB.ProviderName.SqlServer, db);
            akkaConnection.SupportsStringAggregate.Should().BeFalse(
                "SQL Server 2016 does not support STRING_AGG");

            // Act & Assert: StringAggregate should throw when converted to SQL
            var tagTable = db.GetTable<JournalTagRow>();
            var journalTable = db.GetTable<JournalRow>();

            // This is the exact query pattern used in AddTagDataFromTagTableWithStringAggregateAsync
            var act = () => journalTable
                .Select(x => new
                {
                    row = x,
                    tags = tagTable
                        .Where(r => r.OrderingId == x.Ordering)
                        .StringAggregate(";", r => r.TagValue)
                        .ToValue(),
                })
                .ToList();

            act.Should().Throw<Exception>(
                "STRING_AGG is not supported on SQL Server 2016 (compatibility level 130)");
        }
    }
}
