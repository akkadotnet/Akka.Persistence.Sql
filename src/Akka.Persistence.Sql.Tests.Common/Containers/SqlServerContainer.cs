// -----------------------------------------------------------------------
//  <copyright file="SqlServerContainer.cs" company="Akka.NET Project">
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
    ///     Fixture used to run SQL Server
    /// </summary>
    public sealed class SqlServerContainer : DockerContainer
    {
        private const string User = "sa";

        private const string Password = "Password12!";

        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public SqlServerContainer() : base("mcr.microsoft.com/mssql/server", "2019-latest", $"mssql-{Guid.NewGuid():N}")
            => _connectionStringBuilder = new DbConnectionStringBuilder
            {
                ["Server"] = $"localhost,{Port}",
                ["User Id"] = User,
                ["Password"] = Password,
                ["TrustServerCertificate"] = "true",
            };

        public override string ConnectionString => _connectionStringBuilder.ToString();

        public override string ProviderName => LinqToDB.ProviderName.SqlServer2019;

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

            await using var command = new SqlCommand
            {
                CommandText = $"CREATE DATABASE {DatabaseName}",
                Connection = connection,
            };

            await command.ExecuteNonQueryAsync();
            await connection.CloseAsync();
        }
    }
}
