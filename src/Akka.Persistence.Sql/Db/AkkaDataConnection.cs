// -----------------------------------------------------------------------
//  <copyright file="AkkaDataConnection.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Data.RetryPolicy;

namespace Akka.Persistence.Sql.Db
{
    public class AkkaDataConnection : IDisposable, IAsyncDisposable
    {
        private readonly string _providerName;

        public AkkaDataConnection(
            string providerName,
            DataConnection connection)
        {
            _providerName = providerName.ToLower();
            Db = connection;
        }

        public bool UseDateTime =>
            !_providerName.Contains("sqlite") &&
            !_providerName.Contains("postgresql");

        public DataConnection Db { get; }

        public IRetryPolicy RetryPolicy
        {
            get => Db.RetryPolicy;
            set => Db.RetryPolicy = value;
        }

        public ValueTask DisposeAsync()
            => Db.DisposeAsync();

        public void Dispose()
            => Db.Dispose();

        public AkkaDataConnection Clone()
            => new(_providerName, (DataConnection)Db.Clone());

        public ITable<T> GetTable<T>() where T : class
            => Db.GetTable<T>();
    }
}
