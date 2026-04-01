// -----------------------------------------------------------------------
//  <copyright file="SqlServerConnectivityCheckSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2025 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Hosting;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Akka.Persistence.Sql.Tests.Common.Internal.Xunit;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace Akka.Persistence.Sql.Hosting.Tests
{
    [SkipWindows]
    public class SqlServerConnectivityCheckSpec : IAsyncLifetime
    {
        private readonly SqlServerContainer _container;
        private readonly ITestOutputHelper _output;

        public SqlServerConnectivityCheckSpec(ITestOutputHelper output)
        {
            _output = output;
            _container = new SqlServerContainer();
        }

        public async ValueTask InitializeAsync()
        {
            await _container.InitializeAsync();
        }

        public async ValueTask DisposeAsync()
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
                "Server=invalid-host;User Id=invalid;Password=invalid",
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
                "Server=invalid-host;User Id=invalid;Password=invalid",
                _container.ProviderName,
                "sql");

            var context = new AkkaHealthCheckContext(null!);

            // Act
            var result = await check.CheckHealthAsync(context, CancellationToken.None);

            // Assert
            result.Status.Should().Be(HealthStatus.Unhealthy);
            result.Exception.Should().NotBeNull();
        }
    }
}
