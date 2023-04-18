// -----------------------------------------------------------------------
//  <copyright file="MsSqliteContainer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
        private List<SqliteConnection>? _heldConnection;

        public string ConnectionString => $"Filename=file:memdb-{DatabaseName}.db;Mode=Memory;Cache=Shared";

        public string? DatabaseName { get; private set; }

        public string ContainerName => string.Empty;

        public event EventHandler<OutputReceivedArgs>? OnStdOut;

        public DockerClient? Client => null;

        public bool Initialized => _heldConnection is { };

        public string ProviderName => LinqToDB.ProviderName.SQLiteMS;

        public async Task InitializeAsync()
        {
            if (Initialized)
                return;

            _heldConnection = new List<SqliteConnection>();

            await InitializeDbAsync();
        }

        public async Task InitializeDbAsync()
        {
            GenerateDatabaseName();

            var conn = new SqliteConnection(ConnectionString);
            await conn.OpenAsync();

            _heldConnection!.Add(conn);
        }

        public async Task DisposeAsync()
        {
            if (_heldConnection is null)
                return;

            foreach (var conn in _heldConnection)
            {
                conn.Close();
                await conn.DisposeAsync();
            }

            _heldConnection = null;
        }

        public void Dispose()
        {
            if (_heldConnection is null)
                return;

            foreach (var conn in _heldConnection)
            {
                conn.Close();
                conn.Dispose();
            }

            _heldConnection = null;
        }

        private void GenerateDatabaseName()
        {
            DatabaseName = $"sql_tests_{Guid.NewGuid():N}";
        }
    }
}
