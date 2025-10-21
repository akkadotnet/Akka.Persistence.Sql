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
    public class SqlConnectivityCheckSpec : IClassFixture<SqliteContainer>, IAsyncLifetime
    {
        private readonly SqliteContainer _fixture;
        private readonly ITestOutputHelper _output;

        public SqlConnectivityCheckSpec(ITestOutputHelper output, SqliteContainer fixture)
        {
            _output = output;
            _fixture = fixture;
        }

        public async Task InitializeAsync()
        {
            await _fixture.InitializeAsync();
        }

        public async Task DisposeAsync()
        {
            await _fixture.DisposeAsync();
        }

        [Fact]
        public async Task Journal_Connectivity_Check_Should_Return_Healthy_When_Connected()
        {
            // Arrange
            var journalOptions = new SqlJournalOptions(isDefaultPlugin: true)
            {
                ConnectionString = _fixture.ConnectionString,
                ProviderName = _fixture.ProviderName,
                Identifier = "sql"
            };

            var check = new SqlJournalConnectivityCheck(
                journalOptions.ConnectionString,
                journalOptions.ProviderName,
                journalOptions.Identifier);

            var context = new AkkaHealthCheckContext(null!); // ActorSystem not needed for connectivity check

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
                _fixture.ProviderName,
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
            var snapshotOptions = new SqlSnapshotOptions(isDefaultPlugin: true)
            {
                ConnectionString = _fixture.ConnectionString,
                ProviderName = _fixture.ProviderName,
                Identifier = "sql"
            };

            var check = new SqlSnapshotStoreConnectivityCheck(
                snapshotOptions.ConnectionString,
                snapshotOptions.ProviderName,
                snapshotOptions.Identifier);

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
                _fixture.ProviderName,
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
            var action = () => new SqlJournalConnectivityCheck(null!, _fixture.ProviderName, "sql");
            action.Should().Throw<ArgumentNullException>().Where(ex => ex.ParamName == "connectionString");
        }

        [Fact]
        public void Journal_Connectivity_Check_Should_Require_ProviderName()
        {
            // Act & Assert
            var action = () => new SqlJournalConnectivityCheck(_fixture.ConnectionString, null!, "sql");
            action.Should().Throw<ArgumentNullException>().Where(ex => ex.ParamName == "providerName");
        }

        [Fact]
        public void Journal_Connectivity_Check_Should_Require_JournalId()
        {
            // Act & Assert
            var action = () => new SqlJournalConnectivityCheck(_fixture.ConnectionString, _fixture.ProviderName, null!);
            action.Should().Throw<ArgumentNullException>().Where(ex => ex.ParamName == "journalId");
        }

        [Fact]
        public void Snapshot_Connectivity_Check_Should_Require_ConnectionString()
        {
            // Act & Assert
            var action = () => new SqlSnapshotStoreConnectivityCheck(null!, _fixture.ProviderName, "sql");
            action.Should().Throw<ArgumentNullException>().Where(ex => ex.ParamName == "connectionString");
        }

        [Fact]
        public void Snapshot_Connectivity_Check_Should_Require_ProviderName()
        {
            // Act & Assert
            var action = () => new SqlSnapshotStoreConnectivityCheck(_fixture.ConnectionString, null!, "sql");
            action.Should().Throw<ArgumentNullException>().Where(ex => ex.ParamName == "providerName");
        }

        [Fact]
        public void Snapshot_Connectivity_Check_Should_Require_SnapshotStoreId()
        {
            // Act & Assert
            var action = () => new SqlSnapshotStoreConnectivityCheck(_fixture.ConnectionString, _fixture.ProviderName, null!);
            action.Should().Throw<ArgumentNullException>().Where(ex => ex.ParamName == "snapshotStoreId");
        }
    }
}
