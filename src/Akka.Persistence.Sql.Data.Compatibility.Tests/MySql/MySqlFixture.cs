// -----------------------------------------------------------------------
//  <copyright file="MySqlFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Data.Compatibility.Tests.Internal;
using Akka.Util;
using Docker.DotNet.Models;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.MySql
{
    /// <summary>
    ///     Fixture used to run MySql
    /// </summary>
    public sealed class MySqlFixture : DockerContainer
    {
        private string _connectionString = string.Empty;

        public MySqlFixture() : base(
            $"{Const.Repository}/akka-persistence-mysql-test-data",
            "latest",
            $"mysql-{Guid.NewGuid():N}") { }

        public override string ConnectionString => _connectionString;

        private int Port { get; } = ThreadLocalRandom.Current.Next(9000, 10000);

        private string User => "root";

        private string Password => "Password12!";

        protected override string ReadyMarker => "ready for connections. Version";

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
                $"MYSQL_DATABASE={DatabaseName}",
            };
        }

        protected override Task AfterContainerStartedAsync()
        {
            var builder = new DbConnectionStringBuilder
            {
                ["Server"] = "localhost",
                ["Port"] = Port.ToString(),
                ["Database"] = DatabaseName,
                ["User Id"] = User,
                ["Password"] = Password,
            };

            _connectionString = builder.ToString();

            Console.WriteLine($"Connection string: [{_connectionString}]");

            return Task.CompletedTask;
        }
    }
}
