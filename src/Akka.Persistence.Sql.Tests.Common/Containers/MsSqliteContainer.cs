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
        private SqliteConnection? _heldConnection;

        public MsSqliteContainer()
        {
            ConnectionString = $"Filename=file:memdb-{DatabaseName}.db;Mode=Memory;Cache=Shared";

            Console.WriteLine($"Connection string: [{ConnectionString}]");
        }

        public string ConnectionString { get; }

        public string DatabaseName { get; } = $"sql_tests_{Guid.NewGuid():N}";

        public string ContainerName => string.Empty;

        public event EventHandler<OutputReceivedArgs>? OnStdOut;

        public DockerClient? Client => null;

        public bool Initialized { get; private set; }

        public string ProviderName => LinqToDB.ProviderName.SQLiteMS;

        public async Task InitializeAsync()
        {
            if (Initialized)
                return;

            _heldConnection = new SqliteConnection(ConnectionString);
            await _heldConnection.OpenAsync();
            GC.KeepAlive(_heldConnection);

            Initialized = true;
        }

        private static readonly string[] Tables =
        {
            "event_journal",
            "snapshot_store",
            "journal",
            "journal_metadata",
            "tags",
            "snapshot"
        };
        
        public async Task InitializeDbAsync()
        {
            await using var connection = new SqliteConnection(ConnectionString);
            await connection.OpenAsync();

            foreach (var table in Tables)
            {
                await using var command = new SqliteCommand
                {
                    CommandText = $"DROP TABLE IF EXISTS {table};",
                    Connection = connection
                };

                await command.ExecuteNonQueryAsync();
            }
        }

        public async Task DisposeAsync()
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
