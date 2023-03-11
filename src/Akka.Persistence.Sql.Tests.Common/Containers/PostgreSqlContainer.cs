// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlFixture.cs" company="Akka.NET Project">
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
using Npgsql;

namespace Akka.Persistence.Linq2Db.Tests.Common.Containers
{
    /// <summary>
    ///     Fixture used to run Postgre SQL Server
    /// </summary>
    public sealed class PostgreSqlContainer : DockerContainer
    {
        public PostgreSqlContainer() : base("postgres", "latest", $"postgresql-{Guid.NewGuid():N}")
        {
            ConnectionString = new DbConnectionStringBuilder
            {
                ["Server"] = "localhost",
                ["Port"] = Port,
                ["Database"] = DatabaseName,
                ["User Id"] = User,
                ["Password"] = Password
            }.ToString();

            Console.WriteLine($"Connection string: [{ConnectionString}]");
        }

        public override string ConnectionString { get; }

        private int Port { get; } = ThreadLocalRandom.Current.Next(9000, 10000);

        private const string User = "postgres";

        private const string Password = "postgres";

        protected override string ReadyMarker => "ready to accept connections";
        protected override int ReadyCount => 2;

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
                $"POSTGRES_USER={User}",
                $"POSTGRES_DB={DatabaseName}",
            };
        }

        public override async Task InitializeDbAsync()
        {
            await using var conn = new NpgsqlConnection(ConnectionString);
            conn.Open();
            await using var cmd = new NpgsqlCommand
            {
                CommandText = $"SELECT TRUE FROM pg_database WHERE datname='{DatabaseName}'",
                Connection = conn
            };
            var result = cmd.ExecuteScalar();
            var dbExists = result != null && Convert.ToBoolean(result);

            if (dbExists)
            {
                await DoCleanAsync(conn);
            }
            else
            {
                await DoCreateAsync(conn, DatabaseName);
            }

        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task DoCreateAsync(NpgsqlConnection conn, string databaseName)
        {
            await using var cmd = new NpgsqlCommand
            {
                CommandText = $"CREATE DATABASE {databaseName}",
                Connection = conn
            };
            await cmd.ExecuteNonQueryAsync();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task DoCleanAsync(NpgsqlConnection conn)
        {
            await using var cmd = new NpgsqlCommand
            {
                CommandText = @"
DROP TABLE IF EXISTS public.event_journal;
DROP TABLE IF EXISTS public.snapshot_store;
DROP TABLE IF EXISTS public.metadata;

DROP TABLE IF EXISTS public.journal;
DROP TABLE IF EXISTS public.journal_metadata;
DROP TABLE IF EXISTS public.tags;
DROP TABLE IF EXISTS public.snapshot;",
                Connection = conn,
            };
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
