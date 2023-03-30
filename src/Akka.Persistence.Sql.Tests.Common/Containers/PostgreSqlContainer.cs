// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlContainer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Akka.Util;
using Docker.DotNet.Models;
using Npgsql;

namespace Akka.Persistence.Sql.Tests.Common.Containers
{
    /// <summary>
    ///     Fixture used to run PostgreSQL
    /// </summary>
    public sealed class PostgreSqlContainer : DockerContainer
    {
        private const string User = "postgres";

        private const string Password = "postgres";

        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public PostgreSqlContainer() : base("postgres", "latest", $"postgresql-{Guid.NewGuid():N}")
        {
            _connectionStringBuilder = new DbConnectionStringBuilder
            {
                ["Server"] = "localhost",
                ["Port"] = Port,
                ["User Id"] = User,
                ["Password"] = Password
            };
        }

        public override string ConnectionString => _connectionStringBuilder.ToString();

        public override string ProviderName => LinqToDB.ProviderName.PostgreSQL95;

        private int Port { get; } = ThreadLocalRandom.Current.Next(9000, 10000);

        protected override string ReadyMarker => "ready to accept connections";

        protected override int ReadyCount => 2;

        protected override void GenerateDatabaseName()
        {
            base.GenerateDatabaseName();

            _connectionStringBuilder["Database"] = DatabaseName;
        }

        protected override void ConfigureContainer(CreateContainerParameters parameters)
        {
            parameters.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                ["5432/tcp"] = new()
            };

            parameters.HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    ["5432/tcp"] = new List<PortBinding> { new() { HostPort = $"{Port}" } }
                }
            };

            parameters.Env = new[]
            {
                $"POSTGRES_PASSWORD={Password}",
                $"POSTGRES_USER={User}"
            };
        }

        public override async Task InitializeDbAsync()
        {
            var oldName = DatabaseName;
            _connectionStringBuilder["Database"] = "postgres";

            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();

            // PostgreSql creates a connection pool on the __server__ side for __each__ database
            // while having a global maximum connection count.
            // We have to kill the database to drop all these connection pools else we'll get
            // the dreaded "too many client" error.
            if (!string.IsNullOrWhiteSpace(oldName))
            {
                await using var dropCommand = new NpgsqlCommand
                {
                    CommandText = $"DROP DATABASE {oldName} WITH (FORCE)",
                    Connection = connection
                };

                await dropCommand.ExecuteNonQueryAsync();
            }

            GenerateDatabaseName();

            await using var command = new NpgsqlCommand
            {
                CommandText = $"CREATE DATABASE {DatabaseName}",
                Connection = connection
            };

            await command.ExecuteNonQueryAsync();
            await connection.CloseAsync();
        }
    }
}
