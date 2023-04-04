// -----------------------------------------------------------------------
//  <copyright file="AkkaDataConnection.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Journal.Types;
using Akka.Persistence.Sql.Snapshot;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Data.RetryPolicy;
using LinqToDB.DataProvider;
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

            UseDateTime =
                !_providerName.Contains("sqlite") &&
                !_providerName.Contains("postgresql");
        }

        public bool UseDateTime { get; }

        public IDataProvider DataProvider => _connection.DataProvider;

        public IRetryPolicy RetryPolicy
        {
            get => _connection.RetryPolicy;
            set => _connection.RetryPolicy = value;
        }

        public ValueTask DisposeAsync()
            => _connection.DisposeAsync();

        public void Dispose()
            => _connection.Dispose();

        public AkkaDataConnection Clone()
            => new(_providerName, (DataConnection)_connection.Clone());

        public DatabaseSchema GetSchema()
            => _connection.DataProvider.GetSchemaProvider().GetSchema(_connection);

        public ITable<T> CreateTable<T>()
            => _connection.CreateTable<T>();

        public async Task CreateTableAsync<T>(
            TableOptions tableOptions,
            string statementFooter = default,
            CancellationToken cancellationToken = default)
            => await _connection.CreateTableAsync<T>(
                tableOptions: tableOptions,
                statementFooter: statementFooter,
                token: cancellationToken);

        public ITable<T> GetTable<T>() where T : class
            => _connection.GetTable<T>();

        public async Task<DataConnectionTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken = default)
            => await _connection.BeginTransactionAsync(cancellationToken);

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
