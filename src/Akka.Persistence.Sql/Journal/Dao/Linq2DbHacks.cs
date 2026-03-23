// -----------------------------------------------------------------------
//  <copyright file="Linq2DbHacks.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
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
    public static class Linq2DbHacks
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
        public static async Task<List<TOutput>> InsertWithOutputListAsync<TSource, TTarget, TOutput>(
            this IQueryable<TSource> source,
            ITable<TTarget> target,
            [InstantHandle] Expression<Func<TSource, TTarget>> setter,
            Expression<Func<TTarget, TOutput>> outputExpression)
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

            return await currentSource.CreateQuery<TOutput>(expr).ToListAsync();
        }
    }
}
