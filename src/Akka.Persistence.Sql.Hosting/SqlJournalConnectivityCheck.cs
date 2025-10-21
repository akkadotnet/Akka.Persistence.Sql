// -----------------------------------------------------------------------
//  <copyright file="SqlJournalConnectivityCheck.cs" company="Akka.NET Project">
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
    /// Health check that verifies connectivity to the SQL database used by the journal.
    /// This is a liveness check that proactively verifies backend connectivity.
    /// </summary>
    public sealed class SqlJournalConnectivityCheck : IAkkaHealthCheck
    {
        private readonly string _connectionString;
        private readonly string _providerName;
        private readonly string _journalId;

        public SqlJournalConnectivityCheck(string connectionString, string providerName, string journalId)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _providerName = providerName ?? throw new ArgumentNullException(nameof(providerName));
            _journalId = journalId ?? throw new ArgumentNullException(nameof(journalId));
        }

        public async Task<HealthCheckResult> CheckHealthAsync(AkkaHealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                using var db = new DataConnection(_providerName, _connectionString);

                // Execute a simple connectivity test query
                await db.ExecuteAsync("SELECT 1", cancellationToken);

                return HealthCheckResult.Healthy($"SQL journal '{_journalId}' database connection successful");
            }
            catch (OperationCanceledException)
            {
                return HealthCheckResult.Unhealthy($"SQL journal '{_journalId}' database connectivity check timed out");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy(
                    $"SQL journal '{_journalId}' database connection failed",
                    ex);
            }
        }
    }
}
