// -----------------------------------------------------------------------
//  <copyright file="TestFixture.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Tests.Common.Containers;
using Xunit;

namespace Akka.Persistence.Sql.Tests.Common
{
    public class TestFixture : IAsyncLifetime
    {
        private readonly Dictionary<Database, ITestContainer> _containers;

        public TestFixture()
        {
            _containers = new Dictionary<Database, ITestContainer>
            {
                [Database.SqlServer] = new SqlServerContainer(),
                [Database.MySql] = new MySqlContainer(),
                [Database.PostgreSql] = new PostgreSqlContainer(),
                [Database.Sqlite] = new SqliteContainer(),
                [Database.MsSqlite] = new MsSqliteContainer()
            };
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                _containers.Values
                    .Where(container => !container.Initialized)
                    .Select(container => container.InitializeAsync()));
            await Task.WhenAll(
                _containers.Values
                    .Select(container => container.InitializeDbAsync()));
        }

        public async Task DisposeAsync()
            => await Task.WhenAll(_containers.Select(kvp => kvp.Value.DisposeAsync().AsTask()));

        public string ConnectionString(Database database)
            => _containers[database].ConnectionString;

        public async Task InitializeDbAsync(Database database)
        {
            var container = _containers[database];
            if (!container.Initialized)
                await container.InitializeAsync();

            await container.InitializeDbAsync();
        }
    }
}
