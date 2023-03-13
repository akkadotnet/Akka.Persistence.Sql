// -----------------------------------------------------------------------
//  <copyright file="SqliteFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Data.Compatibility.Tests.Internal;
using Docker.DotNet;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.Sqlite
{
    /// <summary>
    ///     Fixture used to run SQL Server
    /// </summary>
    public sealed class SqliteFixture : ITestContainer
    {
        public string ConnectionString { get; private set; } = "";

        public string ContainerName => "";

        public event EventHandler<OutputReceivedArgs>? OnStdOut;

        public DockerClient? Client => null;

        public Task InitializeAsync()
        {
            if (File.Exists("database.db"))
                File.Delete("database.db");
            File.Copy("./db/akka-persistence-sqlite-test-data.latest.db", "database.db");

            ConnectionString = "DataSource=database.db";

            return Task.CompletedTask;
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
