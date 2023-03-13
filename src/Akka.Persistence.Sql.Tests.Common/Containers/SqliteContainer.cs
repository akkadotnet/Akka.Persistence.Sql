// -----------------------------------------------------------------------
//  <copyright file="SqliteContainer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Data.SQLite;
using System.Threading.Tasks;
using Docker.DotNet;

namespace Akka.Persistence.Sql.Tests.Common.Containers
{
    /// <summary>
    ///     Fixture used to run Sqlite
    /// </summary>
    public sealed class SqliteContainer : ITestContainer
    {
        private static SQLiteConnection? _heldConnection;

        public SqliteContainer()
        {
            ConnectionString = $"FullUri=file:{DatabaseName}?mode=memory&cache=shared";

            Console.WriteLine($"Connection string: [{ConnectionString}]");
        }

        public string ConnectionString { get; }

        public string DatabaseName { get; } = $"linq2db_tests_{Guid.NewGuid():N}";

        public string ContainerName => string.Empty;

        public event EventHandler<OutputReceivedArgs>? OnStdOut;

        public DockerClient? Client => null;

        public bool Initialized { get; private set; }

        public async Task InitializeAsync()
        {
            if (Initialized)
                return;

            _heldConnection = new SQLiteConnection(ConnectionString);
            await _heldConnection.OpenAsync();
            GC.KeepAlive(_heldConnection);
            Initialized = true;
        }

        public async Task InitializeDbAsync()
        {
            await using var connection = new SQLiteConnection(ConnectionString);

            await connection.OpenAsync();

            await using var command = new SQLiteCommand
            {
                CommandText = @"
DROP TABLE IF EXISTS event_journal;
DROP TABLE IF EXISTS snapshot;
DROP TABLE IF EXISTS journal_metadata;
DROP TABLE IF EXISTS journal;
DROP TABLE IF EXISTS tags;",
                Connection = connection
            };

            await command.ExecuteNonQueryAsync();
        }

        public async ValueTask DisposeAsync()
        {
            if (_heldConnection is null)
                return;

            _heldConnection.Close();
            await _heldConnection.DisposeAsync();
            _heldConnection = null;
        }

        public void Dispose()
        {
            if (_heldConnection is null)
                return;

            _heldConnection.Close();
            _heldConnection.Dispose();
            _heldConnection = null;
        }
    }
}
