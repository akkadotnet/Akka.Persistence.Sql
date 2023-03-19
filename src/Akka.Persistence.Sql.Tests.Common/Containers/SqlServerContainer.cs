// -----------------------------------------------------------------------
//  <copyright file="SqlServerContainer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Akka.Util;
using Docker.DotNet.Models;

namespace Akka.Persistence.Sql.Tests.Common.Containers
{
    /// <summary>
    ///     Fixture used to run SQL Server
    /// </summary>
    public sealed class SqlServerContainer : DockerContainer
    {
        private const string User = "sa";

        private const string Password = "Password12!";

        public SqlServerContainer() : base("mcr.microsoft.com/mssql/server", "2019-latest", $"mssql-{Guid.NewGuid():N}")
        {
            ConnectionString = new DbConnectionStringBuilder
            {
                ["Server"] = $"localhost,{Port}",
                ["Database"] = DatabaseName,
                ["User Id"] = User,
                ["Password"] = Password,
                ["TrustServerCertificate"] = "true"
            }.ToString();

            Console.WriteLine($"Connection string: [{ConnectionString}]");
        }

        public override string ConnectionString { get; }

        private int Port { get; } = ThreadLocalRandom.Current.Next(9000, 10000);

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

        public override async Task InitializeDbAsync()
        {
            var connectionBuilder = new SqlConnectionStringBuilder(ConnectionString)
            {
                // connect to SqlServer database to create a new database
                InitialCatalog = "master"
            };

            var initConnectionString = connectionBuilder.ToString();

            await using var connection = new SqlConnection(initConnectionString);

            await connection.OpenAsync();

            await using var command = new SqlCommand
            {
                CommandText = @$"
IF db_id('{DatabaseName}') IS NULL
BEGIN
    CREATE DATABASE {DatabaseName}
END",
                Connection = connection
            };

            command.ExecuteScalar();

            await DropTablesAsync(connection, DatabaseName);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static async Task DropTablesAsync(SqlConnection connection, string databaseName)
        {
            await using var command = new SqlCommand
            {
                CommandText = $@"
USE {databaseName};
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'EventJournal') BEGIN DROP TABLE dbo.EventJournal END;
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'Metadata') BEGIN DROP TABLE dbo.Metadata END;
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'SnapshotStore') BEGIN DROP TABLE dbo.SnapshotStore END;

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'journal') BEGIN DROP TABLE dbo.journal END;
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'journal_metadata') BEGIN DROP TABLE dbo.journal_metadata END;
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'tags') BEGIN DROP TABLE dbo.tags END;
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'snapshot') BEGIN DROP TABLE dbo.snapshot END;

IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'customJournalTable') BEGIN DROP TABLE dbo.customJournalTable END;
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'customMetadataTable') BEGIN DROP TABLE dbo.customMetadataTable END;",
                Connection = connection
            };

            await command.ExecuteNonQueryAsync();
        }
    }
}
