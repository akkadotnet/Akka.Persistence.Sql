// -----------------------------------------------------------------------
//  <copyright file="TestFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Persistence.Linq2Db.Tests.Common.Containers;
using Xunit;

namespace Akka.Persistence.Linq2Db.Tests.Common
{
    public class TestFixture: IAsyncLifetime
    {
        private readonly Dictionary<Database, ITestContainer> _containers;

        public TestFixture()
        {
            _containers = new Dictionary<Database, ITestContainer>
            {
                [Database.SqlServer] = new SqlServerContainer(),
                [Database.MySql] = new MySqlContainer(),
                [Database.Postgres] = new PostgreSqlContainer(),
                [Database.SqLite] = new SqliteContainer(),
                [Database.MsSqLite] = new MsSqliteContainer()
            };
        }

        public string ConnectionString(Database mode) => _containers[mode].ConnectionString;

        public Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            await Task.WhenAll(_containers.Select(kvp => kvp.Value.DisposeAsync().AsTask()));
        }

        public async Task InitializeDbAsync(Database mode)
        {
            var container = _containers[mode];
            if (!container.Initialized)
                await container.InitializeAsync();
            await container.InitializeDbAsync();
        }
    }
}