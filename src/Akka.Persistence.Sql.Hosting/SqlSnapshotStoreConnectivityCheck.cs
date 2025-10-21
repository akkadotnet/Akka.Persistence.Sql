// -----------------------------------------------------------------------
//  <copyright file="SqlSnapshotStoreConnectivityCheck.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2025 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Hosting;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Akka.Persistence.Sql.Hosting
{
    /// <summary>
    /// Health check that verifies connectivity to the SQL database used by the snapshot store.
    /// This is a liveness check that proactively verifies backend connectivity.
    /// </summary>
    public sealed class SqlSnapshotStoreConnectivityCheck : IAkkaHealthCheck
    {
        private readonly string _connectionString;
        private readonly string _providerName;
        private readonly string _snapshotStoreId;

        public SqlSnapshotStoreConnectivityCheck(string connectionString, string providerName, string snapshotStoreId)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _providerName = providerName ?? throw new ArgumentNullException(nameof(providerName));
            _snapshotStoreId = snapshotStoreId ?? throw new ArgumentNullException(nameof(snapshotStoreId));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(AkkaHealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                using var db = new DataConnection(_providerName, _connectionString);

                // Execute a simple connectivity test query
                await db.ExecuteAsync("SELECT 1", cancellationToken);

                return HealthCheckResult.Healthy($"SQL snapshot store '{_snapshotStoreId}' database connection successful");
            }
            catch (OperationCanceledException)
            {
                return HealthCheckResult.Unhealthy($"SQL snapshot store '{_snapshotStoreId}' database connectivity check timed out");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    $"SQL snapshot store '{_snapshotStoreId}' database connection failed",
                    ex);
            }
        }
    }
}
