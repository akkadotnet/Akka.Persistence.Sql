// -----------------------------------------------------------------------
//  <copyright file="Linq2DbExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LinqToDB;
using LinqToDB.Async;
using LinqToDB.Internal.Async;
using LinqToDB.Internal.Linq;

namespace Akka.Persistence.Sql.Journal.Dao
{
    public class InstantHandleAttribute : Attribute
    {
        
    }
    public static class Linq2DbExtensions
    {
        public static IQueryable<T> ProcessIQueryable<T>(this IQueryable<T> source)
        {
            return (IQueryable<T>)(LinqExtensions.ProcessSourceQueryable?.Invoke(source) ?? source);
        }
        
        #if NET6_0_OR_GREATER
        [DoesNotReturn]
#endif
        private static IQueryProviderAsync ThrowInvalidSource(string? method)
        {
            throw new LinqToDBException($"LinqToDB method '{method}' called on non-LinqToDB IQueryable.");
        }
        
        public static IQueryProviderAsync GetLinqToDBSource<T>(this IQueryable<T> source, [CallerMemberName] string? method = null)
        {
            if (source.ProcessIQueryable() is not IQueryProviderAsync query)
                return ThrowInvalidSource(method);

            return query;
        }

        public static void ThrowIfNull(
            #if NET6_0_OR_GREATER
            [NotNull]
#endif
            object? argument, string? paramName = null)
        {
            if (argument is null)
                throw new ArgumentNullException(paramName);
        }

        /// <summary>
        /// Inserts data from a source queryable into a target table and retrieves a list of output projections.
        /// </summary>
        /// <typeparam name="TSource">The type of elements in the source queryable.</typeparam>
        /// <typeparam name="TTarget">The type of the target table where data is inserted.</typeparam>
        /// <typeparam name="TOutput">The type of elements in the output result list.</typeparam>
        /// <param name="source">The source queryable containing the data to insert into the target table.</param>
        /// <param name="target">The target database table where data is inserted.</param>
        /// <param name="setter">
        /// An expression that maps source entities to target entities for the insert operation.
        /// </param>
        /// <param name="outputExpression">
        /// An expression that specifies the projection for the output result list.
        /// </param>
        /// <returns>A task representing the asynchronous operation. The task result is a list of output projections.</returns>
        /// <remarks>
        /// Yeah so weird thing the existing Linq2Db InsertWithOutput only returns IAsyncEnumerable<T> but does weird syncish jank.
        /// This runs better in general.
        /// </remarks>
        public static async Task<List<TOutput>> InsertWithOutputListAsync<TSource, TTarget, TOutput>(
            this IQueryable<TSource> source,
            ITable<TTarget> target,
            [InstantHandle] Expression<Func<TSource, TTarget>> setter,
            Expression<Func<TTarget, TOutput>> outputExpression,
            CancellationToken token = default)
            where TTarget : notnull
        {
#if NET6_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(setter);
            ArgumentNullException.ThrowIfNull(outputExpression); 
#else
            ThrowIfNull(source, "source");
            ThrowIfNull(target, "target");
            ThrowIfNull(setter, "setter");
            ThrowIfNull(outputExpression, "outputExpression"); 

#endif
            var currentSource = source.GetLinqToDBSource();

            var expr = Expression.Call(
                null,
                MethodHelper.GetMethodInfo(LinqToDB.LinqExtensions.InsertWithOutput, source, target, setter, outputExpression),
                currentSource.Expression,
                ((IQueryable<TTarget>)target).Expression,
                Expression.Quote(setter),
                Expression.Quote(outputExpression));

            return await currentSource.CreateQuery<TOutput>(expr).ToListAsync(token);
        }
    }
}
