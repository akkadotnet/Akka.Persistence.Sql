// -----------------------------------------------------------------------
//  <copyright file="ConnectionFactoryExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence.Sql.Db;
using Akka.Persistence.Sql.Query;
using Akka.Persistence.Sql.Query.Dao;

namespace Akka.Persistence.Sql.Extensions
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

        internal static async Task<T> ExecuteQueryWithTransactionAsync<T>(
            this AkkaPersistenceDataConnectionFactory factory,
            DbStateHolder state,
            Func<AkkaDataConnection, CancellationToken, Task<T>> handler)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(state.ShutdownToken);
            {
                cts.CancelAfter(state.QueryThrottleTimeout);
                await state.QueryPermitter.Ask<QueryStartGranted>(RequestQueryStart.Instance, cts.Token);
            }
            
            try
            {
                return await factory.ExecuteWithTransactionAsync(state.IsolationLevel, state.ShutdownToken, handler);
            }
            finally
            {
                state.QueryPermitter.Tell(ReturnQueryStart.Instance);
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
        
        internal static async Task<T> ExecuteQueryWithTransactionAsync<TState,T>(
            this DbStateHolder factory,
            TState state,
            Func<AkkaDataConnection, CancellationToken, TState, Task<T>> handler)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(factory.ShutdownToken);
            {
                cts.CancelAfter(factory.QueryThrottleTimeout);
                await factory.QueryPermitter.Ask<QueryStartGranted>(RequestQueryStart.Instance, cts.Token);
            }
            
            try
            {
                return await factory.ConnectionFactory.ExecuteWithTransactionAsync(state, factory.IsolationLevel, factory.ShutdownToken, handler);
            }
            finally
            {
                factory.QueryPermitter.Tell(ReturnQueryStart.Instance);
            }
        }
        
        public static async Task<T> ExecuteWithTransactionAsync<TState,T>(
            this AkkaPersistenceDataConnectionFactory factory,
            TState state,
            IsolationLevel level,
            CancellationToken token,
            Func<AkkaDataConnection, CancellationToken, TState, Task<T>> handler)
        {
            await using var connection = factory.GetConnection();
            await using var tx = await connection.BeginTransactionAsync(level, token);

            try
            {
                var result = await handler(connection, token, state);
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
