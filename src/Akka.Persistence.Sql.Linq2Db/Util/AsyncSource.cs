using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Streams.Dsl;
using Akka.Util;

namespace Akka.Persistence.Sql.Linq2Db.Utility
{
    /// <summary>
    /// Creates Sources from Async Functions
    /// The Async function will be evaluated anew
    /// For each Materialization
    /// </summary>
    /// <typeparam name="TElem"></typeparam>
    public static class AsyncSource<TElem>
    {
        /// <summary>
        /// Creates a Source using an Async function's result as input
        /// The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="func">The Async function producing a value</param>
        /// <returns></returns>
        public static Source<TElem, NotUsed> From(Func<Task<TElem>> func)
        {
            return AsyncSource.From(func);
        }

        /// <summary>
        /// Creates a Source using an Async function's result as input
        /// The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="state">The input state passed to the async function</param>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> From<TState>(TState state,
            Func<TState, Task<TElem>> func)
        {
            return AsyncSource.From<TState, TElem>(state, func);
        }
        /// <summary>
        /// Creates a Source using an Async function's result as input,
        /// Flattening an enumerable out to a stream of individual elements
        /// The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> FromEnumerable(
            Func<Task<IEnumerable<TElem>>> func)
        {
            return AsyncSource.FromEnumerable(func);
        }
        /// <summary>
        /// Creates a Source using an Async function's result as input,
        /// Flattening an enumerable out to a stream of individual elements
        /// The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="state">The input state passed to the async function</param>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> FromEnumerable<TState>(TState state,
            Func<TState,Task<IEnumerable<TElem>>> func)
        {
            return AsyncSource.FromEnumerable<TState, TElem>(state, func);
        }
    }
    public static class AsyncSource
    {
        /// <summary>
        /// Creates a Source using an Async function's result as input
        /// The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> From<TElem>(
            Func<Task<TElem>> func)
        {
            return Source.Single(NotUsed.Instance)
                .SelectAsync(1,  notUsed =>  func());
        }
        /// <summary>
        /// Creates a Source using an Async function's result as input
        /// The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="state">The input state passed to the async function</param>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> From<TState,TElem>(TState state,
            Func<TState,Task<TElem>> func)
        {
            return Source.Single(state)
                .SelectAsync(1,  func);
        }
        /// <summary>
        /// Creates a Source using an Async function's result as input,
        /// Flattening an enumerable out to a stream of individual elements
        /// The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> FromEnumerable<TElem>(
            Func<Task<IEnumerable<TElem>>> func)
        {
            return Source.Single(NotUsed.Instance)
                .SelectAsync(1, notUsed => func())
                .SelectMany(r => r);
        }
        
        /// <summary>
        /// Creates a Source using an Async function's result as input,
        /// Flattening an enumerable out to a stream of individual elements
        /// The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="state">The input state passed to the async function</param>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> FromEnumerable<TState,TElem>(TState state,
            Func<TState,Task<IEnumerable<TElem>>> func)
        {
            return Source.Single(state)
                .SelectAsync(1, f=> func(f))
                .SelectMany(r => r);
        }
    }
}