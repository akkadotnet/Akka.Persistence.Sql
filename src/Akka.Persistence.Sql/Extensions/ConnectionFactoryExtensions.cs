// -----------------------------------------------------------------------
//  <copyright file="ConnectionFactoryExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Akka.Persistence.Sql.Db
{
    public static class ConnectionFactoryExtensions
    {
        public static async Task ExecuteWithTransactionAsync(
            this AkkaPersistenceDataConnectionFactory factory,
            IsolationLevel level,
            CancellationToken token,
            Func<AkkaDataConnection, CancellationToken, Task> handler)
        {
            await using var connection = factory.GetConnection();
            await using var tx = await connection.BeginTransactionAsync(level, token);
            try
            {
                await handler(connection, token);
                await tx.CommitAsync(token);
            }
            catch (Exception ex1)
            {
                try
                {
                    await tx.RollbackAsync(token);
                }
                catch (Exception ex2)
                {
                    throw new AggregateException("Exception thrown when rolling back database transaction", ex2, ex1);
                }
                throw;
            }
        }
        
        public static async Task<T> ExecuteWithTransactionAsync<T>(
            this AkkaPersistenceDataConnectionFactory factory,
            IsolationLevel level,
            CancellationToken token,
            Func<AkkaDataConnection, CancellationToken, Task<T>> handler)
        {
            await using var connection = factory.GetConnection();
            await using var tx = await connection.BeginTransactionAsync(level, token);
            try
            {
                var result = await handler(connection, token);
                await tx.CommitAsync(token);
                return result;
            }
            catch (Exception ex1)
            {
                try
                {
                    await tx.RollbackAsync(token);
                }
                catch (Exception ex2)
                {
                    throw new AggregateException("Exception thrown when rolling back database transaction", ex2, ex1);
                }
                throw;
            }
        }
    }
}
