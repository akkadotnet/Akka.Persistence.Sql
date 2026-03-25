// -----------------------------------------------------------------------
//  <copyright file="SupportsStringAggregateSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Db;
using FluentAssertions;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Settings
{
    public class SupportsStringAggregateSpec
    {
        [Theory(DisplayName = "SQL Server versions before 2017 should not support StringAggregate")]
        [InlineData(SqlServerVersion.v2005)]
        [InlineData(SqlServerVersion.v2008)]
        [InlineData(SqlServerVersion.v2012)]
        [InlineData(SqlServerVersion.v2014)]
        [InlineData(SqlServerVersion.v2016)]
        public void SqlServerBelow2017ShouldNotSupportStringAggregate(SqlServerVersion version)
        {
            var dataProvider = SqlServerTools.GetDataProvider(version);
            using var dataConnection = new DataConnection(dataProvider, "Server=fake;Database=fake;");
            var akkaConnection = new AkkaDataConnection(LinqToDB.ProviderName.SqlServer, dataConnection);

            akkaConnection.SupportsStringAggregate.Should().BeFalse(
                $"SQL Server {version} does not support STRING_AGG");
        }

        [Theory(DisplayName = "SQL Server 2017 and above should support StringAggregate")]
        [InlineData(SqlServerVersion.v2017)]
        [InlineData(SqlServerVersion.v2019)]
        [InlineData(SqlServerVersion.v2022)]
        public void SqlServer2017AndAboveShouldSupportStringAggregate(SqlServerVersion version)
        {
            var dataProvider = SqlServerTools.GetDataProvider(version);
            using var dataConnection = new DataConnection(dataProvider, "Server=fake;Database=fake;");
            var akkaConnection = new AkkaDataConnection(LinqToDB.ProviderName.SqlServer, dataConnection);

            akkaConnection.SupportsStringAggregate.Should().BeTrue(
                $"SQL Server {version} supports STRING_AGG");
        }

        [Fact(DisplayName = "Non-SQL Server providers should always support StringAggregate")]
        public void NonSqlServerProvidersShouldSupportStringAggregate()
        {
            // Use a PostgreSQL provider as representative non-SQL Server provider
            var dataProvider = LinqToDB.DataProvider.PostgreSQL.PostgreSQLTools.GetDataProvider(
                LinqToDB.DataProvider.PostgreSQL.PostgreSQLVersion.v15);
            using var dataConnection = new DataConnection(dataProvider, "Host=fake;Database=fake;");
            var akkaConnection = new AkkaDataConnection(LinqToDB.ProviderName.PostgreSQL15, dataConnection);

            akkaConnection.SupportsStringAggregate.Should().BeTrue(
                "non-SQL Server providers support STRING_AGG or GROUP_CONCAT equivalents");
        }
    }
}
