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
        /// <summary>
        /// <see cref="Exception.Data"/> key under which cleanup failures (transaction rollback/dispose,
        /// connection dispose) encountered during a replay-safe transaction retry attempt are attached to
        /// the primary operation exception. The value is either a single <see cref="Exception"/> or an
        /// <see cref="AggregateException"/> when more than one cleanup failure was recorded. Cleanup
        /// failures are never allowed to replace or mask the primary exception, since the configured retry
        /// policy relies on that exception to decide whether to attempt another transaction.
        /// </summary>
        public const string CleanupExceptionDataKey = "Akka.Persistence.Sql.TransactionCleanupException";

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
            try
            {
                await state.QueryPermitter.Ask<QueryStartGranted>(new RequestQueryStart(state.QueryThrottleTimeout), state.QueryThrottleTimeout);
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
            try
            {
                await factory.QueryPermitter.Ask<QueryStartGranted>(new RequestQueryStart(factory.QueryThrottleTimeout), factory.QueryThrottleTimeout);
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

        /// <summary>
        /// Executes a replay-safe operation with the configured retry policy outside the transaction
        /// boundary. Every policy attempt owns a new connection and transaction.
        /// </summary>
        internal static async Task ExecuteReplaySafeWithTransactionRetryAsync(
            this AkkaPersistenceDataConnectionFactory factory,
            IsolationLevel level,
            CancellationToken token,
            Func<AkkaDataConnection, CancellationToken, Task> handler,
            ILoggingAdapter? logger = null)
        {
            if (!factory.HasRetryPolicy)
            {
                await factory.ExecuteWithTransactionAsync(level, token, handler);
                return;
            }

            var retryPolicy = factory.TakeReplayRetryPolicy();

            async Task ExecuteAttempt(CancellationToken cancellationToken)
            {
                var connection = factory.GetConnectionWithoutRetryPolicy();
                Exception? operationException = null;

                try
                {
                    await ExecuteTransactionAttemptAsync(connection, level, cancellationToken, handler, logger);
                }
                catch (Exception exception)
                {
                    operationException = exception;
                    throw;
                }
                finally
                {
                    await DisposeConnectionAsync(connection, operationException, logger);
                }
            }

            if (retryPolicy is null)
                await ExecuteAttempt(token);
            else
                await retryPolicy.ExecuteAsync(ExecuteAttempt, token);
        }

        private static async Task ExecuteTransactionAttemptAsync(
            AkkaDataConnection connection,
            IsolationLevel level,
            CancellationToken token,
            Func<AkkaDataConnection, CancellationToken, Task> handler,
            ILoggingAdapter? logger)
        {
            var tx = await connection.BeginTransactionAsync(level, token);

            try
            {
                await handler(connection, token);
                await tx.CommitAsync(token);
            }
            catch (Exception operationException)
            {
                try
                {
                    await tx.RollbackAsync(token);
                }
                catch (Exception rollbackException)
                {
                    // Cleanup failures must not hide the exception the configured policy uses to
                    // decide whether to run a new transaction attempt.
                    AttachCleanupException(operationException, rollbackException);
                    logger?.Warning(
                        rollbackException,
                        "Transaction cleanup failed after a journal operation error; the original operation exception is preserved and remains the primary error.");
                }

                try
                {
                    await tx.DisposeAsync();
                }
                catch (Exception disposeException)
                {
                    AttachCleanupException(operationException, disposeException);
                    logger?.Warning(
                        disposeException,
                        "Transaction cleanup failed after a journal operation error; the original operation exception is preserved and remains the primary error.");
                }

                throw;
            }

            await tx.DisposeAsync();
        }

        private static async Task DisposeConnectionAsync(
            AkkaDataConnection connection,
            Exception? operationException,
            ILoggingAdapter? logger)
        {
            try
            {
                await connection.DisposeAsync();
            }
            catch (Exception cleanupException) when (operationException is not null)
            {
                AttachCleanupException(operationException, cleanupException);
                logger?.Warning(
                    cleanupException,
                    "Transaction cleanup failed after a journal operation error; the original operation exception is preserved and remains the primary error.");
            }
        }

        private static void AttachCleanupException(Exception operationException, Exception cleanupException)
        {
            try
            {
                if (operationException.Data[CleanupExceptionDataKey] is Exception previousCleanupException)
                {
                    operationException.Data[CleanupExceptionDataKey] = new AggregateException(
                        previousCleanupException,
                        cleanupException);
                }
                else
                {
                    operationException.Data[CleanupExceptionDataKey] = cleanupException;
                }
            }
            catch
            {
                // Exception.Data can be read-only for custom exception implementations.
            }
        }
    }
}
