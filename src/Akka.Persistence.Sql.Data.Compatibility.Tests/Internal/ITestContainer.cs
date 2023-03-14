// -----------------------------------------------------------------------
//  <copyright file="ITestContainer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Docker.DotNet;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.Internal
{
    public interface ITestContainer : IDisposable, IAsyncDisposable
    {
        public string ConnectionString { get; }

        public string DatabaseName => "akka_persistence_tests";

        public string ContainerName { get; }

        public DockerClient? Client { get; }

        public event EventHandler<OutputReceivedArgs> OnStdOut;

        public Task InitializeAsync();
    }
}
