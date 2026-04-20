// -----------------------------------------------------------------------
//  <copyright file="SqlServer2016Container.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Akka.Util;
using Docker.DotNet.Models;
using Microsoft.Data.SqlClient;

namespace Akka.Persistence.Sql.Tests.Common.Containers
{
    /// <summary>
    /// Runs a SQL Server 2022 container but sets the database compatibility level to 130
    /// (SQL Server 2016) so that LinqToDB auto-detects it as SQL Server 2016.
    /// This means STRING_AGG is not available, forcing the client-side tag aggregation fallback.
    /// Uses the generic "SqlServer" provider name to trigger LinqToDB auto-detection.
    /// </summary>
    public sealed class SqlServer2016Container : DockerContainer
    {
        private const string User = "sa";

        private const string Password = "Password12!";

        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public SqlServer2016Container() : base("mcr.microsoft.com/mssql/server", "2022-latest", $"mssql2016-{Guid.NewGuid():N}")
            => _connectionStringBuilder = new DbConnectionStringBuilder
            {
                ["Server"] = $"localhost,{Port}",
                ["User Id"] = User,
                ["Password"] = Password,
                ["TrustServerCertificate"] = "true",
            };

        public override string ConnectionString => _connectionStringBuilder.ToString();

        // Use generic "SqlServer" so LinqToDB auto-detects version from compatibility level
        public override string ProviderName => LinqToDB.ProviderName.SqlServer;

        private int Port { get; } = ThreadLocalRandom.Current.Next(9000, 10000);

        protected override string ReadyMarker => "Recovery is complete.";

        protected override void GenerateDatabaseName()
        {
            base.GenerateDatabaseName();

            _connectionStringBuilder["Database"] = DatabaseName;
        }

        protected override void ConfigureContainer(CreateContainerParameters parameters)
        {
            parameters.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                ["1433/tcp"] = new(),
            };

            parameters.HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    ["1433/tcp"] = new List<PortBinding> { new() { HostPort = $"{Port}" } },
                },
            };

            parameters.Env = new[]
            {
                "ACCEPT_EULA=Y",
                $"MSSQL_SA_PASSWORD={Password}",
                "MSSQL_PID=Express",
            };
        }

        public override async Task InitializeDbAsync()
        {
            _connectionStringBuilder["Database"] = "master";

            await using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            GenerateDatabaseName();

            await using var createCommand = new SqlCommand
            {
                CommandText = $"CREATE DATABASE [{DatabaseName}]",
                Connection = connection,
            };
            await createCommand.ExecuteNonQueryAsync();

            // Set compatibility level to 130 (SQL Server 2016)
            // This causes LinqToDB to auto-detect the version as SQL Server 2016,
            // which does not support STRING_AGG
            await using var compatCommand = new SqlCommand
            {
                CommandText = $"ALTER DATABASE [{DatabaseName}] SET COMPATIBILITY_LEVEL = 130",
                Connection = connection,
            };
            await compatCommand.ExecuteNonQueryAsync();

            await connection.CloseAsync();
        }
    }
}
