// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Akka.Persistence.Linq2Db.Data.Compatibility.Tests.Internal;
using Akka.Util;
using Docker.DotNet.Models;
using Xunit;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.PostgreSql
{
    /// <summary>
    ///     Fixture used to run Postgre SQL Server
    /// </summary>
    public sealed class PostgreSqlFixture : DockerContainer
    {
        public PostgreSqlFixture() : base($"{Const.Repository}/akka-persistence-postgresql-test-data", "latest", $"postgresql-{Guid.NewGuid():N}")
        {
        }

        private string _connectionString = "";
        public override string ConnectionString => _connectionString;

        private int Port { get; } = ThreadLocalRandom.Current.Next(9000, 10000);

        private string User { get; } = "postgres";

        private string Password { get; } = "postgres";

        protected override string ReadyMarker => "ready to accept connections";

        protected override void ConfigureContainer(CreateContainerParameters parameters)
        {
            parameters.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                {"5432/tcp", new ()}
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
            };
        }

        protected override Task AfterContainerStartedAsync()
        {
            var builder = new DbConnectionStringBuilder
            {
                ["Server"] = "localhost",
                ["Port"] = Port,
                ["Database"] = DatabaseName,
                ["User Id"] = User,
                ["Password"] = Password
            };

            _connectionString = builder.ToString();

            Console.WriteLine($"Connection string: [{_connectionString}]");

            return Task.CompletedTask;
        }

    }
}
