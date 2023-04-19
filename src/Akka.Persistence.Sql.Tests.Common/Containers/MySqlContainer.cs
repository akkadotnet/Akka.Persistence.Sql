// -----------------------------------------------------------------------
//  <copyright file="MySqlContainer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Akka.Util;
using Docker.DotNet.Models;
using MySql.Data.MySqlClient;

namespace Akka.Persistence.Sql.Tests.Common.Containers
{
    /// <summary>
    ///     Fixture used to run MySql
    /// </summary>
    public sealed class MySqlContainer : DockerContainer
    {
        private const string User = "root";

        private const string Password = "Password12!";

        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public MySqlContainer() : base("mysql", "8", $"mysql-{Guid.NewGuid():N}")
            => _connectionStringBuilder = new DbConnectionStringBuilder
            {
                ["Server"] = "localhost",
                ["Port"] = Port.ToString(),
                ["User Id"] = User,
                ["Password"] = Password,
                ["allowPublicKeyRetrieval"] = "true",
                ["Allow User Variables"] = "true",
            };

        public override string ConnectionString => _connectionStringBuilder.ToString();

        public override string ProviderName => LinqToDB.ProviderName.MySqlOfficial;

        private int Port { get; } = ThreadLocalRandom.Current.Next(9000, 10000);

        protected override string ReadyMarker => "ready for connections. Version";

        protected override void GenerateDatabaseName()
        {
            base.GenerateDatabaseName();

            _connectionStringBuilder["Database"] = DatabaseName;
        }

        protected override void ConfigureContainer(CreateContainerParameters parameters)
        {
            parameters.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                ["3306/tcp"] = new(),
            };

            parameters.HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    ["3306/tcp"] = new List<PortBinding> { new() { HostPort = $"{Port}" } },
                },
            };

            parameters.Env = new[]
            {
                $"MYSQL_ROOT_PASSWORD={Password}",
            };
        }

        protected override async Task AfterContainerStartedAsync()
        {
            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand
            {
                CommandText = "SET GLOBAL max_connections = 999;",
                Connection = connection,
            };
            await command.ExecuteNonQueryAsync();
            await connection.CloseAsync();

            await base.AfterContainerStartedAsync();
        }

        public override async Task InitializeDbAsync()
        {
            _connectionStringBuilder["Database"] = "sys";

            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            if (!string.IsNullOrWhiteSpace(DatabaseName))
            {
                try
                {
                    await using var dropCommand = new MySqlCommand
                    {
                        CommandText = @$"DROP DATABASE IF EXISTS `{DatabaseName}`;",
                        Connection = connection,
                    };
                    await dropCommand.ExecuteNonQueryAsync();
                }
                catch
                {
                    // no-op
                }
            }

            GenerateDatabaseName();

            await using var command = new MySqlCommand
            {
                CommandText = $"CREATE DATABASE `{DatabaseName}`;",
                Connection = connection,
            };
            await command.ExecuteNonQueryAsync();
            await connection.CloseAsync();
        }
    }
}
