// -----------------------------------------------------------------------
//  <copyright file="ITestContainer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Docker.DotNet;

namespace Akka.Persistence.Linq2Db.Tests.Common.Containers
{
    public interface ITestContainer: IDisposable, IAsyncDisposable
    {
        public string ConnectionString { get; }
        public string DatabaseName { get; }
        public string ContainerName { get; }
        public event EventHandler<OutputReceivedArgs> OnStdOut;
        public DockerClient? Client { get; }
        public Task InitializeAsync();
        public Task InitializeDbAsync();
        public bool Initialized { get; }
    }
}