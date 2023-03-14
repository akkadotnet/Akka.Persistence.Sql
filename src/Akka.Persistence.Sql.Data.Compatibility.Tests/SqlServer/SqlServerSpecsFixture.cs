// -----------------------------------------------------------------------
//  <copyright file="SqlServerSpecsFixture.cs" company="Akka.NET Project">
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

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.SqlServer
{
    /// <summary>
    ///     Fixture used to run SQL Server
    /// </summary>
    public sealed class SqlServerFixture : DockerContainer
    {
        private string _connectionString = string.Empty;

        public SqlServerFixture() : base(
            $"{Const.Repository}/akka-persistence-sqlserver-test-data",
            "latest",
            $"mssql-{Guid.NewGuid():N}") { }

        public override string ConnectionString => _connectionString;

        private int Port { get; } = ThreadLocalRandom.Current.Next(9000, 10000);

        private string User => "sa";

        private string Password => "Password12!";

        protected override string ReadyMarker => "Recovery is complete.";

        protected override void ConfigureContainer(CreateContainerParameters parameters)
        {
            parameters.ExposedPorts = new Dictionary<string, EmptyStruct>
            {
                ["1433/tcp"] = new()
            };

            parameters.HostConfig = new HostConfig
            {
                PortBindings = new Dictionary<string, IList<PortBinding>>
                {
                    ["1433/tcp"] = new List<PortBinding> { new() { HostPort = $"{Port}" } }
                }
            };

            parameters.Env = new[]
            {
                "ACCEPT_EULA=Y",
                $"MSSQL_SA_PASSWORD={Password}",
                "MSSQL_PID=Express"
            };
        }

        protected override Task AfterContainerStartedAsync()
        {
            var builder = new DbConnectionStringBuilder
            {
                ["Server"] = $"localhost,{Port}",
                ["Database"] = DatabaseName,
                ["User Id"] = User,
                ["Password"] = Password,
                ["TrustServerCertificate"] = "true"
            };

            _connectionString = builder.ToString();

            Console.WriteLine($"Connection string: [{_connectionString}]");

            return Task.CompletedTask;
        }
    }
}
