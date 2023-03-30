// -----------------------------------------------------------------------
//  <copyright file="MySqlContainer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Runtime.CompilerServices;
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

        public MySqlContainer() : base("mysql", "latest", $"mysql-{Guid.NewGuid():N}")
        {
            _connectionStringBuilder = new DbConnectionStringBuilder
            {
                ["Server"] = "localhost",
                ["Port"] = Port.ToString(),
                ["User Id"] = User,
                ["Password"] = Password
            };
        }

        public override string ConnectionString => _connectionStringBuilder.ToString();

        public override string ProviderName => LinqToDB.ProviderName.MySql;

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
                ["3306/tcp"] = new()
            };

            parameters.HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    ["3306/tcp"] = new List<PortBinding> { new() { HostPort = $"{Port}" } }
                }
            };

            parameters.Env = new[]
            {
                $"MYSQL_ROOT_PASSWORD={Password}",
                $"MYSQL_DATABASE={DatabaseName}"
            };
        }

        public override async Task InitializeDbAsync()
        {
            GenerateDatabaseName();

            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();

            await using var command = new MySqlCommand
            {
                CommandText = $"CREATE DATABASE {DatabaseName}",
                Connection = connection
            };

            await command.ExecuteNonQueryAsync();
            await connection.CloseAsync();
        }
    }
}
