// -----------------------------------------------------------------------
//  <copyright file="SqliteFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Docker.DotNet;
using Microsoft.Data.Sqlite;

namespace Akka.Persistence.Linq2Db.Tests.Common.Containers
{
    /// <summary>
    ///     Fixture used to run SQL Server
    /// </summary>
    public sealed class MsSqliteContainer : ITestContainer
    {
        private static SqliteConnection? _heldConnection;

        public string ConnectionString { get; }
        public string DatabaseName { get; } = $"linq2db_tests_{Guid.NewGuid():N}";

        public string ContainerName => "";

        public event EventHandler<OutputReceivedArgs>? OnStdOut;

        public DockerClient? Client => null;

        public bool Initialized => true;

        public MsSqliteContainer()
        {
            ConnectionString = $"Filename=file:memdb-{DatabaseName}.db;Mode=Memory;Cache=Shared";

            if(_heldConnection is null)
            {
                _heldConnection = new SqliteConnection(ConnectionString);
                _heldConnection.Open();
                GC.KeepAlive(_heldConnection);
            }
        }

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task InitializeDbAsync()
        {
            await using var conn = new SqliteConnection(ConnectionString);
            conn.Open();

            await using var cmd = new SqliteCommand
            {
                CommandText = @"
DROP TABLE IF EXISTS event_journal;
DROP TABLE IF EXISTS journal_metadata;
DROP TABLE IF EXISTS snapshot;

DROP TABLE IF EXISTS journal;
DROP TABLE IF EXISTS tags;
",
                Connection = conn
            };

            await cmd.ExecuteNonQueryAsync();
        }

        public ValueTask DisposeAsync()
        {
            // no-op
            return new ValueTask();
        }

        public void Dispose()
        {
            // no-op
        }
    }
}