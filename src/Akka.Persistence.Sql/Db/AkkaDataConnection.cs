// -----------------------------------------------------------------------
//  <copyright file="AkkaDataConnection.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Snapshot;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider;
using LinqToDB.DataProvider.SqlServer;
using LinqToDB.Internal.DataProvider.SqlServer;
using LinqToDB.SchemaProvider;

namespace Akka.Persistence.Sql.Db
{
    public class AkkaDataConnection : IDisposable, IAsyncDisposable
    {
        private readonly DataConnection _connection;
        private readonly string _providerName;

        public AkkaDataConnection(
            string providerName,
            DataConnection connection)
        {
            _providerName = providerName.ToLower();
            _connection = connection;

            UseDateTime = _providerName.ToLowerInvariant().Contains("sqlserver");
        }

        public bool UseDateTime { get; }

        public IDataProvider DataProvider => _connection.DataProvider;

        /// <summary>
        /// Checks whether the resolved data provider supports SQL-level string aggregation
        /// (STRING_AGG for SQL Server 2017+, GROUP_CONCAT for MySQL/SQLite).
        /// Uses LinqToDB's auto-detected <see cref="SqlServerVersion"/> when available.
        /// </summary>
        internal bool SupportsStringAggregate
        {
            get
            {
                if (_connection.DataProvider is SqlServerDataProvider sqlServerProvider)
                    return sqlServerProvider.Version >= SqlServerVersion.v2017;

                // All non-SQL Server providers (PostgreSQL, MySQL, SQLite) support StringAggregate
                return true;
            }
        }

        public ValueTask DisposeAsync()
            => _connection.DisposeAsync();

        public void Dispose()
            => _connection.Dispose();

        public AkkaDataConnection Clone()
            => new AkkaDataConnection(_providerName, new DataConnection(_connection.Options));

        public DatabaseSchema GetSchema()
            => _connection.DataProvider.GetSchemaProvider().GetSchema(_connection);

        public ITable<T> CreateTable<T>() where T : notnull
            => _connection.CreateTable<T>();

        public async Task CreateTableAsync<T>(
            TableOptions tableOptions,
            string? statementFooter = default,
            CancellationToken cancellationToken = default) where T : notnull
            => await _connection.CreateTableAsync<T>(
                tableOptions: tableOptions,
                statementFooter: statementFooter,
                token: cancellationToken);

        public ITable<T> GetTable<T>() where T : class
            => _connection.GetTable<T>();
        
        public IQueryable<T> AsQueryable<T>(IEnumerable<T> set) where T : class
            => set.AsQueryable(_connection);

        public IQueryable<T> SelectQuery<T>(Expression<Func<T>> expr) where T : class
            => _connection.SelectQuery(expr);

        public async Task<DataConnectionTransaction> BeginTransactionAsync(
            IsolationLevel isolationLevel,
            CancellationToken cancellationToken = default)
            => await _connection.BeginTransactionAsync(isolationLevel, cancellationToken);

        public async Task<int> InsertAsync(
            JournalRow journalRow,
            CancellationToken cancellationToken = default)
            => await _connection.InsertAsync(journalRow, token: cancellationToken);

        public async Task<long> InsertWithInt64IdentityAsync(
            JournalRow journalRow,
            CancellationToken cancellationToken = default)
            => await _connection.InsertWithInt64IdentityAsync(journalRow, token: cancellationToken);

        public async Task<int> InsertOrReplaceAsync(
            DateTimeSnapshotRow dateTimeSnapshotRow,
            CancellationToken cancellationToken = default)
            => await _connection.InsertOrReplaceAsync(dateTimeSnapshotRow, token: cancellationToken);

        public async Task<int> InsertOrReplaceAsync(
            LongSnapshotRow longSnapshotRow,
            CancellationToken cancellationToken = default)
            => await _connection.InsertOrReplaceAsync(longSnapshotRow, token: cancellationToken);

        public async Task CommitTransactionAsync(
            CancellationToken cancellationToken = default)
            => await _connection.CommitTransactionAsync(cancellationToken);

        public async Task RollbackTransactionAsync(
            CancellationToken cancellationToken = default)
            => await _connection.RollbackTransactionAsync(cancellationToken);
    }
}
