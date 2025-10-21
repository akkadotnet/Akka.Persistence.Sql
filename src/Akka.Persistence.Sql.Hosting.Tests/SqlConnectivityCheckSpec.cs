// -----------------------------------------------------------------------
//  <copyright file="SqlConnectivityCheckSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2025 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Hosting;
using Akka.Persistence.Sql.Tests.Common.Containers;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Hosting.Tests
{
    public class SqliteConnectivityCheckSpec : IAsyncLifetime
    {
        private readonly SqliteContainer _container;
        private readonly ITestOutputHelper _output;

        public SqliteConnectivityCheckSpec(ITestOutputHelper output)
        {
            _output = output;
            _container = new SqliteContainer();
        }

        public async Task InitializeAsync()
        {
            await _container.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await _container.DisposeAsync();
        }

        [Fact]
        public async Task Journal_Connectivity_Check_Should_Return_Healthy_When_Connected()
        {
            // Arrange
            var check = new SqlJournalConnectivityCheck(
                _container.ConnectionString,
                _container.ProviderName,
                "sql");

            var context = new AkkaHealthCheckContext(null!);

            // Act
            var result = await check.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(HealthStatus.Healthy);
            result.Description.Should().Contain("successful");
        }

        [Fact]
        public async Task Journal_Connectivity_Check_Should_Return_Unhealthy_When_Disconnected()
        {
            // Arrange
            var check = new SqlJournalConnectivityCheck(
                "invalid-connection-string",
                _container.ProviderName,
                "sql");

            var context = new AkkaHealthCheckContext(null!);

            // Act
            var result = await check.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Exception.Should().NotBeNull();
        }

        [Fact]
        public async Task Snapshot_Connectivity_Check_Should_Return_Healthy_When_Connected()
        {
            // Arrange
            var check = new SqlSnapshotStoreConnectivityCheck(
                _container.ConnectionString,
                _container.ProviderName,
                "sql");

            var context = new AkkaHealthCheckContext(null!);

            // Act
            var result = await check.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(HealthStatus.Healthy);
            result.Description.Should().Contain("successful");
        }

        [Fact]
        public async Task Snapshot_Connectivity_Check_Should_Return_Unhealthy_When_Disconnected()
        {
            // Arrange
            var check = new SqlSnapshotStoreConnectivityCheck(
                "invalid-connection-string",
                _container.ProviderName,
                "sql");

            var context = new AkkaHealthCheckContext(null!);

            // Act
            var result = await check.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Exception.Should().NotBeNull();
        }

        [Fact]
        public void Journal_Connectivity_Check_Should_Require_ConnectionString()
        {
            // Act & Assert
            var action = () => new SqlJournalConnectivityCheck(null!, "SQLiteClassic", "sql");
            action.Should().Throw<ArgumentNullException>().Where(ex => ex.ParamName == "connectionString");
        }

        [Fact]
        public void Journal_Connectivity_Check_Should_Require_ProviderName()
        {
            // Act & Assert
            var action = () => new SqlJournalConnectivityCheck("valid-connection", null!, "sql");
            action.Should().Throw<ArgumentNullException>().Where(ex => ex.ParamName == "providerName");
        }

        [Fact]
        public void Journal_Connectivity_Check_Should_Require_JournalId()
        {
            // Act & Assert
            var action = () => new SqlJournalConnectivityCheck("valid-connection", "SQLiteClassic", null!);
            action.Should().Throw<ArgumentNullException>().Where(ex => ex.ParamName == "journalId");
        }

        [Fact]
        public void Snapshot_Connectivity_Check_Should_Require_ConnectionString()
        {
            // Act & Assert
            var action = () => new SqlSnapshotStoreConnectivityCheck(null!, "SQLiteClassic", "sql");
            action.Should().Throw<ArgumentNullException>().Where(ex => ex.ParamName == "connectionString");
        }

        [Fact]
        public void Snapshot_Connectivity_Check_Should_Require_ProviderName()
        {
            // Act & Assert
            var action = () => new SqlSnapshotStoreConnectivityCheck("valid-connection", null!, "sql");
            action.Should().Throw<ArgumentNullException>().Where(ex => ex.ParamName == "providerName");
        }

        [Fact]
        public void Snapshot_Connectivity_Check_Should_Require_SnapshotStoreId()
        {
            // Act & Assert
            var action = () => new SqlSnapshotStoreConnectivityCheck("valid-connection", "SQLiteClassic", null!);
            action.Should().Throw<ArgumentNullException>().Where(ex => ex.ParamName == "snapshotStoreId");
        }
    }
}
