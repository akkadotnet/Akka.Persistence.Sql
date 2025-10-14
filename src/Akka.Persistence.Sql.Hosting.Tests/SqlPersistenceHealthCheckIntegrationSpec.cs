// -----------------------------------------------------------------------
//  <copyright file="SqlPersistenceHealthCheckIntegrationSpec.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Hosting;
using Akka.Hosting.HealthChecks;
using Akka.Persistence.Journal;
using Akka.Persistence.Snapshot;
using Akka.Persistence.Sql.Journal;
using Akka.Persistence.Sql.Snapshot;
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
    /// Integration tests that verify health checks work correctly with hosting extensions
    /// and properly expose the custom CheckHealthAsync implementations.
    /// </summary>
    public class SqlPersistenceHealthCheckIntegrationSpec : Akka.Hosting.TestKit.TestKit, IClassFixture<SqliteContainer>
    {
        private readonly SqliteContainer _fixture;

        public SqlPersistenceHealthCheckIntegrationSpec(ITestOutputHelper output, SqliteContainer fixture)
            : base(nameof(SqlPersistenceHealthCheckIntegrationSpec), output)
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
            // Configure SQL persistence with health checks enabled
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
        public async Task Health_checks_should_call_custom_CheckHealthAsync_implementations()
        {
            // Arrange
            var healthCheckService = Host.Services.GetRequiredService<HealthCheckService>();
            var actorSystem = Host.Services.GetRequiredService<ActorSystem>();

            // Get the actual journal and snapshot store instances
            var persistence = Persistence.Instance.Apply(actorSystem);
            var journal = persistence.JournalFor(null);
            var snapshotStore = persistence.SnapshotStoreFor(null);

            journal.Should().NotBeNull("Journal actor should exist");
            snapshotStore.Should().NotBeNull("SnapshotStore actor should exist");

            // Act - run health checks through the hosting service
            var healthReport = await healthCheckService.CheckHealthAsync(CancellationToken.None);

            // Assert - verify health checks are registered and healthy
            healthReport.Entries.Should().NotBeEmpty();
            healthReport.Status.Should().Be(HealthStatus.Healthy);

            // Find persistence health checks
            var persistenceHealthChecks = healthReport.Entries
                .Where(e => e.Key.Contains("Akka.Persistence", StringComparison.OrdinalIgnoreCase))
                .ToList();

            persistenceHealthChecks.Should().HaveCount(2, "Both journal and snapshot health checks should be registered");

            // Verify journal health check
            var journalHealth = persistenceHealthChecks
                .FirstOrDefault(e => e.Key.Contains("journal", StringComparison.OrdinalIgnoreCase));

            journalHealth.Should().NotBeNull();
            journalHealth.Value.Status.Should().Be(HealthStatus.Healthy);

            // Verify snapshot health check
            var snapshotHealth = persistenceHealthChecks
                .FirstOrDefault(e => e.Key.Contains("snapshot", StringComparison.OrdinalIgnoreCase));

            snapshotHealth.Should().NotBeNull();
            snapshotHealth.Value.Status.Should().Be(HealthStatus.Healthy);

            Output?.WriteLine($"Journal health check: {journalHealth.Key} - {journalHealth.Value.Status}");
            Output?.WriteLine($"Snapshot health check: {snapshotHealth.Key} - {snapshotHealth.Value.Status}");
        }

        [Fact]
        public async Task Health_checks_should_directly_test_database_connectivity()
        {
            // Arrange
            var actorSystem = Host.Services.GetRequiredService<ActorSystem>();
            var persistence = Persistence.Instance.Apply(actorSystem);
            var journal = persistence.JournalFor(null);
            var snapshotStore = persistence.SnapshotStoreFor(null);

            // Act - directly call CheckHealthAsync on both components
            var journalHealthTask = journal.Ask<JournalHealthCheckResponse>(new CheckJournalHealth(CancellationToken.None));
            var snapshotHealthTask = snapshotStore.Ask<SnapshotStoreHealthCheckResponse>(new CheckSnapshotStoreHealth(CancellationToken.None));

            // Assert
            var journalResult = await journalHealthTask;
            var snapshotResult = await snapshotHealthTask;

            // Both should return healthy status with database connection verified
            journalResult.Result.Status.Should().Be(PersistenceHealthStatus.Healthy);
            journalResult.Result.Description.Should().Be("Ok");

            snapshotResult.Result.Status.Should().Be(PersistenceHealthStatus.Healthy);
            snapshotResult.Result.Description.Should().Be("Ok");
        }

        [Fact]
        public async Task Health_checks_should_integrate_with_aspnetcore_health_checks()
        {
            // Arrange
            var healthCheckService = Host.Services.GetRequiredService<HealthCheckService>();

            // Act - perform health check with predicate to filter only persistence checks
            var healthReport = await healthCheckService.CheckHealthAsync(
                predicate: registration => registration.Name.Contains("Akka.Persistence", StringComparison.OrdinalIgnoreCase),
                cancellationToken: CancellationToken.None);

            // Assert
            healthReport.Entries.Should().HaveCount(2, "Should only have journal and snapshot checks");
            healthReport.Status.Should().Be(HealthStatus.Healthy);

            foreach (var entry in healthReport.Entries)
            {
                Output?.WriteLine($"Health check '{entry.Key}':");
                Output?.WriteLine($"  Status: {entry.Value.Status}");
                Output?.WriteLine($"  Duration: {entry.Value.Duration}");

                if (entry.Value.Data?.Any() == true)
                {
                    Output?.WriteLine($"  Data: {string.Join(", ", entry.Value.Data.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                }
            }
        }
    }
}