// -----------------------------------------------------------------------
//  <copyright file="SqliteSpecsFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
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
    ///     Fixture used to run Sqlite
    /// </summary>
    public sealed class SqliteFixture : ITestContainer
    {
        public string ConnectionString { get; private set; } = string.Empty;

        public string ContainerName => string.Empty;

        public event EventHandler<OutputReceivedArgs>? OnStdOut;

        public DockerClient? Client => null;

        public Task InitializeAsync()
        {
            if (File.Exists("database.db"))
                File.Delete("database.db");

            File.Copy("./db/akka-persistence-sqlite-test-data.latest.db", "database.db");

            ConnectionString = "DataSource=database.db";

            Console.WriteLine($"Connection string: [{ConnectionString}]");

            return Task.CompletedTask;
        }

        // no-op
        public ValueTask DisposeAsync() => new();

        // no-op
        public void Dispose() { }
    }
}
