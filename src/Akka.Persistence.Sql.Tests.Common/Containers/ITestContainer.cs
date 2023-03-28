// -----------------------------------------------------------------------
//  <copyright file="ITestContainer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Docker.DotNet;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Common.Containers
{
    public interface ITestContainer : IDisposable, IAsyncLifetime
    {
        public string ConnectionString { get; }

        public string DatabaseName { get; }

        public string ContainerName { get; }

        public DockerClient? Client { get; }

        public bool Initialized { get; }
        
        public string ProviderName { get; }

        public event EventHandler<OutputReceivedArgs> OnStdOut;

        public Task InitializeDbAsync();
    }
}
