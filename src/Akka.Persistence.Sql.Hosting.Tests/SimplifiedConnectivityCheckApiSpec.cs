// -----------------------------------------------------------------------
//  <copyright file="SimplifiedConnectivityCheckApiSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2025 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Hosting;
using Akka.Persistence.Sql.Tests.Common.Containers;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Akka.Persistence.Sql.Hosting.Tests
{
    /// <summary>
    /// Tests for the simplified Akka.Hosting 1.5.55.1+ API where options are automatically
    /// accessed from builder.Options without requiring explicit parameter passing.
    /// </summary>
    public class SimplifiedConnectivityCheckApiSpec : Akka.Hosting.TestKit.TestKit, IClassFixture<SqliteContainer>
    {
        private readonly SqliteContainer _fixture;

        public SimplifiedConnectivityCheckApiSpec(ITestOutputHelper output, SqliteContainer fixture)
            : base(nameof(SimplifiedConnectivityCheckApiSpec), output)
        {
            _fixture = fixture;

            if (!_fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
        }

        protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            base.ConfigureServices(context, services);
            services.AddHealthChecks();
        }

        protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            // Use the simplified API - this matches the user's configuration pattern
            builder.WithSqlPersistence(
                connectionString: _fixture.ConnectionString,
                providerName: _fixture.ProviderName,
                autoInitialize: true,
                journalBuilder: journal =>
                {
                    journal.WithHealthCheck();
                    // NEW SIMPLIFIED API: No need to pass options!
                    journal.WithConnectivityCheck();
                },
                snapshotBuilder: snapshot =>
                {
                    snapshot.WithHealthCheck();
                    // NEW SIMPLIFIED API: No need to pass options!
                    snapshot.WithConnectivityCheck();
                });
        }

        [Fact]
        public async Task Simplified_API_Should_Register_Both_Standard_And_Connectivity_Health_Checks()
        {
            // Arrange
            var healthCheckService = Host.Services.GetRequiredService<HealthCheckService>();

            // Act
            var healthReport = await healthCheckService.CheckHealthAsync(CancellationToken.None);

            // Assert - we should have 4 health checks total:
            // 1. Journal standard health check
            // 2. Journal connectivity check
            // 3. Snapshot standard health check
            // 4. Snapshot connectivity check

            var persistenceHealthChecks = healthReport.Entries
                .Where(e => e.Key.Contains("Akka.Persistence", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Debug output
            Output?.WriteLine($"Total Akka.Persistence health checks: {persistenceHealthChecks.Count}");
            foreach (var check in persistenceHealthChecks)
            {
                Output?.WriteLine($"  - {check.Key}: {check.Value.Status}");
            }

            persistenceHealthChecks.Should().HaveCount(4,
                "because we registered standard + connectivity checks for both journal and snapshot");

            // Verify all are healthy
            foreach (var check in persistenceHealthChecks)
            {
                check.Value.Status.Should().Be(HealthStatus.Healthy);
            }
        }

        [Fact]
        public async Task Journal_Connectivity_Check_Should_Be_Registered()
        {
            // Arrange
            var healthCheckService = Host.Services.GetRequiredService<HealthCheckService>();

            // Act
            var healthReport = await healthCheckService.CheckHealthAsync(CancellationToken.None);

            // Assert
            var journalConnectivityCheck = healthReport.Entries
                .FirstOrDefault(e => e.Key.Contains("Journal") && e.Key.Contains("Connectivity"));

            journalConnectivityCheck.Should().NotBeNull("journal connectivity check should be registered");
            journalConnectivityCheck.Value.Status.Should().Be(HealthStatus.Healthy,
                "database connection should be valid");
            journalConnectivityCheck.Value.Description.Should().Contain("successful");
        }

        [Fact]
        public async Task Snapshot_Connectivity_Check_Should_Be_Registered()
        {
            // Arrange
            var healthCheckService = Host.Services.GetRequiredService<HealthCheckService>();

            // Act
            var healthReport = await healthCheckService.CheckHealthAsync(CancellationToken.None);

            // Assert
            var snapshotConnectivityCheck = healthReport.Entries
                .FirstOrDefault(e => e.Key.Contains("SnapshotStore") && e.Key.Contains("Connectivity"));

            snapshotConnectivityCheck.Should().NotBeNull("snapshot connectivity check should be registered");
            snapshotConnectivityCheck.Value.Status.Should().Be(HealthStatus.Healthy,
                "database connection should be valid");
            snapshotConnectivityCheck.Value.Description.Should().Contain("successful");
        }

        [Fact]
        public async Task Connectivity_Checks_Should_Use_Default_Names()
        {
            // Arrange
            var healthCheckService = Host.Services.GetRequiredService<HealthCheckService>();

            // Act
            var healthReport = await healthCheckService.CheckHealthAsync(CancellationToken.None);

            // Assert - verify default naming convention
            var journalConnectivity = healthReport.Entries
                .Where(e => e.Key.StartsWith("Akka.Persistence.Sql.Journal.") && e.Key.EndsWith(".Connectivity"))
                .ToList();

            journalConnectivity.Should().HaveCount(1,
                "journal connectivity check should use default naming");

            var snapshotConnectivity = healthReport.Entries
                .Where(e => e.Key.StartsWith("Akka.Persistence.Sql.SnapshotStore.") && e.Key.EndsWith(".Connectivity"))
                .ToList();

            snapshotConnectivity.Should().HaveCount(1,
                "snapshot connectivity check should use default naming");
        }
    }

    /// <summary>
    /// Tests for custom health check configuration with the simplified API.
    /// </summary>
    public class CustomConnectivityCheckConfigSpec : Akka.Hosting.TestKit.TestKit, IClassFixture<SqliteContainer>
    {
        private readonly SqliteContainer _fixture;

        public CustomConnectivityCheckConfigSpec(ITestOutputHelper output, SqliteContainer fixture)
            : base(nameof(CustomConnectivityCheckConfigSpec), output)
        {
            _fixture = fixture;

            if (!_fixture.InitializeDbAsync().Wait(10.Seconds()))
                throw new Exception("Failed to clean up database in 10 seconds");
        }

        protected override void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            base.ConfigureServices(context, services);
            services.AddHealthChecks();
        }

        protected override void ConfigureAkka(AkkaConfigurationBuilder builder, IServiceProvider provider)
        {
            // Use the simplified API with custom names and tags
            builder.WithSqlPersistence(
                connectionString: _fixture.ConnectionString,
                providerName: _fixture.ProviderName,
                autoInitialize: true,
                journalBuilder: journal =>
                {
                    journal.WithConnectivityCheck(
                        name: "custom-journal-connectivity",
                        tags: new[] { "custom", "backend", "database" });
                },
                snapshotBuilder: snapshot =>
                {
                    snapshot.WithConnectivityCheck(
                        name: "custom-snapshot-connectivity",
                        tags: new[] { "custom", "backend", "database" });
                });
        }

        [Fact]
        public async Task Should_Use_Custom_Names()
        {
            // Arrange
            var healthCheckService = Host.Services.GetRequiredService<HealthCheckService>();

            // Act
            var healthReport = await healthCheckService.CheckHealthAsync(CancellationToken.None);

            // Assert
            var customChecks = healthReport.Entries
                .Where(e => e.Key.Contains("custom"))
                .ToList();

            customChecks.Should().HaveCount(2, "should have both custom journal and snapshot connectivity checks");

            // Verify the custom journal connectivity check exists and is healthy
            healthReport.Entries.Keys.Should().Contain("custom-journal-connectivity");
            healthReport.Entries["custom-journal-connectivity"].Status.Should().Be(HealthStatus.Healthy);

            // Verify the custom snapshot connectivity check exists and is healthy
            healthReport.Entries.Keys.Should().Contain("custom-snapshot-connectivity");
            healthReport.Entries["custom-snapshot-connectivity"].Status.Should().Be(HealthStatus.Healthy);
        }
    }
}
