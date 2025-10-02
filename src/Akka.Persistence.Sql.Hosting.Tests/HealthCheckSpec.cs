// -----------------------------------------------------------------------
//  <copyright file="HealthCheckSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Hosting;
using Akka.Hosting.HealthChecks;
using Akka.Persistence.Sql.Tests.Common.Containers;
using FluentAssertions;
using FluentAssertions.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Hosting.Tests
{
    /// <summary>
    /// Validates that health checks are properly registered after the refactoring
    /// </summary>
    public class HealthCheckSpec : Akka.Hosting.TestKit.TestKit, IClassFixture<SqliteContainer>
    {
        private readonly SqliteContainer _fixture;

        public HealthCheckSpec(ITestOutputHelper output, SqliteContainer fixture)
            : base(nameof(HealthCheckSpec), output)
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
            // Use the refactored WithSqlPersistence with health check registration
            builder.WithSqlPersistence(
                connectionString: _fixture.ConnectionString,
                providerName: _fixture.ProviderName,
                journalBuilder: journal =>
                {
                    journal.WithHealthCheck(HealthStatus.Degraded);
                },
                snapshotBuilder: snapshot =>
                {
                    snapshot.WithHealthCheck(HealthStatus.Degraded);
                });
        }

        [Fact]
        public async Task Health_checks_should_be_registered_and_healthy()
        {
            // Arrange
            var healthCheckService = Host.Services.GetRequiredService<HealthCheckService>();

            // Act - run all health checks
            var healthReport = await healthCheckService.CheckHealthAsync(CancellationToken.None);

            // Assert - verify that health checks are registered and healthy
            healthReport.Entries.Should().NotBeEmpty("health checks should be registered");

            // Debug: print all registered health checks
            Output?.WriteLine($"Total health checks registered: {healthReport.Entries.Count}");
            foreach (var entry in healthReport.Entries)
            {
                Output?.WriteLine($"  - {entry.Key}: {entry.Value.Status}");
            }

            // We should have at least 1 health check for SQL persistence
            var sqlHealthChecks = healthReport.Entries
                .Where(e => e.Key.Contains("sql", StringComparison.OrdinalIgnoreCase))
                .ToList();

            sqlHealthChecks.Should().HaveCountGreaterOrEqualTo(1,
                "because we registered health checks for SQL persistence");

            // Verify all SQL health checks are healthy
            foreach (var healthCheck in sqlHealthChecks)
            {
                healthCheck.Value.Status.Should().Be(HealthStatus.Healthy,
                    $"because {healthCheck.Key} should be properly initialized");
            }

            // Verify overall health status
            healthReport.Status.Should().Be(HealthStatus.Healthy,
                "because all health checks should pass");
        }
    }
}
