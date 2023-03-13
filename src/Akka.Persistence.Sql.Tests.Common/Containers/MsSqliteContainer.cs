// -----------------------------------------------------------------------
//  <copyright file="MsSqliteContainer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Docker.DotNet;
using Microsoft.Data.Sqlite;

namespace Akka.Persistence.Sql.Tests.Common.Containers
{
    /// <summary>
    ///     Fixture used to run Sqlite
    /// </summary>
    public sealed class MsSqliteContainer : ITestContainer
    {
        private static SqliteConnection? _heldConnection;

        public MsSqliteContainer()
        {
            ConnectionString = $"Filename=file:memdb-{DatabaseName}.db;Mode=Memory;Cache=Shared";

            Console.WriteLine($"Connection string: [{ConnectionString}]");

            if (_heldConnection is not null)
                return;

            _heldConnection = new SqliteConnection(ConnectionString);
            _heldConnection.Open();

            GC.KeepAlive(_heldConnection);
        }

        public string ConnectionString { get; }

        public string DatabaseName { get; } = $"linq2db_tests_{Guid.NewGuid():N}";

        public string ContainerName => string.Empty;

        public event EventHandler<OutputReceivedArgs>? OnStdOut;

        public DockerClient? Client => null;

        public bool Initialized => true;

        public Task InitializeAsync()
            => Task.CompletedTask;

        public async Task InitializeDbAsync()
        {
            await using var connection = new SqliteConnection(ConnectionString);

            await connection.OpenAsync();

            await using var command = new SqliteCommand
            {
                CommandText = @"
DROP TABLE IF EXISTS event_journal;
DROP TABLE IF EXISTS journal_metadata;
DROP TABLE IF EXISTS snapshot;

DROP TABLE IF EXISTS journal;
DROP TABLE IF EXISTS tags;
",
                Connection = connection
            };

            await command.ExecuteNonQueryAsync();
        }

        // no-op
        public ValueTask DisposeAsync()
            => new();

        // no-op
        public void Dispose() { }
    }
}
