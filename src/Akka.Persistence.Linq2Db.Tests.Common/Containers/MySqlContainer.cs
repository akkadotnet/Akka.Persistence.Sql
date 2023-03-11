// -----------------------------------------------------------------------
//  <copyright file="MySqlFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
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

namespace Akka.Persistence.Linq2Db.Tests.Common.Containers
{
    /// <summary>
    ///     Fixture used to run MySql SQL Server
    /// </summary>
    public sealed class MySqlContainer : DockerContainer
    {
        public MySqlContainer() : base("mysql", "latest", $"mysql-{Guid.NewGuid():N}")
        {
            ConnectionString = new DbConnectionStringBuilder
            {
                ["Server"] = "localhost",
                ["Port"] = Port.ToString(),
                ["Database"] = DatabaseName,
                ["User Id"] = User,
                ["Password"] = Password
            }.ToString();

            Console.WriteLine($"Connection string: [{ConnectionString}]");
        }

        public override string ConnectionString { get; }

        private int Port { get; } = ThreadLocalRandom.Current.Next(9000, 10000);

        private const string User = "root";

        private const string Password = "Password12!";

        protected override string ReadyMarker => "ready for connections. Version";

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
                $"MYSQL_DATABASE={DatabaseName}",
            };
        }

        public override async Task InitializeDbAsync()
        {
            await using var conn = new MySqlConnection(ConnectionString);
            conn.Open();

            await using var cmd = new MySqlCommand
            {
                CommandText = $"SELECT true FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{DatabaseName}'",
                Connection = conn
            };
            var result = cmd.ExecuteScalar();
            var dbExists = result != null && Convert.ToBoolean(result);

            if (dbExists)
            {
                await DropTablesAsync(conn);
            }
            else
            {
                await DoCreateAsync(conn, DatabaseName);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task DoCreateAsync(MySqlConnection conn, string databaseName)
        {
            await using var cmd = new MySqlCommand
            {
                CommandText = $@"CREATE DATABASE {databaseName}",
                Connection = conn
            };
            await cmd.ExecuteNonQueryAsync();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task DropTablesAsync(MySqlConnection conn)
        {
            await using var cmd = new MySqlCommand
            {
                CommandText = @"
DROP TABLE IF EXISTS event_journal;
DROP TABLE IF EXISTS snapshot_store;
DROP TABLE IF EXISTS metadata;
DROP TABLE IF EXISTS journal;
DROP TABLE IF EXISTS journal_metadata;
DROP TABLE IF EXISTS tags;
DROP TABLE IF EXISTS snapshot;",
                Connection = conn
            };

            await cmd.ExecuteNonQueryAsync();
        }
    }
}