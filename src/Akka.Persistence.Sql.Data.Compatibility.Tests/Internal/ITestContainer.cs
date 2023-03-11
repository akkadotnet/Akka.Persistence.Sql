// -----------------------------------------------------------------------
//  <copyright file="ITestContainer.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Docker.DotNet;
using Xunit;

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.Internal
{
    public interface ITestContainer: IDisposable, IAsyncDisposable
    {
        public string ConnectionString { get; }

        public virtual string DatabaseName => "akka_persistence_tests";

        public string ContainerName { get; }

        public event EventHandler<OutputReceivedArgs> OnStdOut;

        public DockerClient? Client { get; }

        public Task InitializeAsync();
    }
}
