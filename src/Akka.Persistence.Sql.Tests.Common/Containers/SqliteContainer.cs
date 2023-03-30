// -----------------------------------------------------------------------
//  <copyright file="SqliteContainer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
        private List<SQLiteConnection>? _heldConnection;

        public string ConnectionString => $"FullUri=file:memdb-{DatabaseName}?mode=memory&cache=shared";

        public string DatabaseName { get; private set; } = string.Empty;

        public string ContainerName => string.Empty;

        public string ProviderName => LinqToDB.ProviderName.SQLiteClassic;

        public event EventHandler<OutputReceivedArgs>? OnStdOut;

        public DockerClient? Client => null;

        public bool Initialized => _heldConnection is { };

        private void GenerateDatabaseName()
        {
            DatabaseName = $"sql_tests_{Guid.NewGuid():N}";
        }
        
        public async Task InitializeAsync()
        {
            if (Initialized)
                return;

            _heldConnection = new List<SQLiteConnection>();
            await InitializeDbAsync();
        }

        public async Task InitializeDbAsync()
        {
            GenerateDatabaseName();
            var conn = new SQLiteConnection(ConnectionString); 
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
    }
}
