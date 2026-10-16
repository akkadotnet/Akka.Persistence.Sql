// -----------------------------------------------------------------------
//  <copyright file="AsyncSource.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Akka.Streams.Dsl;

namespace Akka.Persistence.Sql.Utility
{
    /// <summary>
    ///     Creates Sources from Async Functions
    ///     The Async function will be evaluated anew
    ///     For each Materialization
    /// </summary>
    /// <typeparam name="TElem"></typeparam>
    public static class AsyncSource<TElem>
    {
        /// <summary>
        ///     Creates a Source using an Async function's result as input
        ///     The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="func">The Async function producing a value</param>
        /// <returns></returns>
        public static Source<TElem, NotUsed> From(Func<Task<TElem>> func)
            => AsyncSource.From(func);

        /// <summary>
        ///     Creates a Source using an Async function's result as input
        ///     The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="state">The input state passed to the async function</param>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> From<TState>(
            TState state,
            Func<TState, Task<TElem>> func)
            => AsyncSource.From(state, func);

        /// <summary>
        ///     Creates a Source using an Async function's result as input,
        ///     Flattening an enumerable out to a stream of individual elements
        ///     The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> FromEnumerable(
            Func<Task<IEnumerable<TElem>>> func)
            => AsyncSource.FromEnumerable(func);

        /// <summary>
        ///     Creates a Source using an Async function's result as input,
        ///     Flattening an enumerable out to a stream of individual elements
        ///     The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="state">The input state passed to the async function</param>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> FromEnumerable<TState>(
            TState state,
            Func<TState, Task<IEnumerable<TElem>>> func)
            => AsyncSource.FromEnumerable(state, func);

        /// <summary>
        ///     Like <see cref="FromEnumerable{TState}(TState, Func{TState, Task{IEnumerable{TElem}}})"/>, but the
        ///     function gets a token that is cancelled when the materialized stream stops, so that the work it
        ///     started (e.g. a database query) is cancelled together with the stream.
        /// </summary>
        internal static Source<TElem, NotUsed> FromEnumerable<TState>(
            TState state,
            Func<TState, CancellationToken, Task<IEnumerable<TElem>>> func)
            => Source.From(() => Enumerate(state, func));

        private static async IAsyncEnumerable<TElem> Enumerate<TState>(
            TState state,
            Func<TState, CancellationToken, Task<IEnumerable<TElem>>> func,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            foreach (var element in await func(state, token))
                yield return element;
        }
    }

    public static class AsyncSource
    {
        /// <summary>
        ///     Creates a Source using an Async function's result as input
        ///     The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> From<TElem>(
            Func<Task<TElem>> func)
            => Source
                .Single(NotUsed.Instance)
                .SelectAsync(1, _ => func());

        /// <summary>
        ///     Creates a Source using an Async function's result as input
        ///     The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="state">The input state passed to the async function</param>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> From<TState, TElem>(
            TState state,
            Func<TState, Task<TElem>> func)
            => Source
                .Single(state)
                .SelectAsync(1, func);

        /// <summary>
        ///     Creates a Source using an Async function's result as input,
        ///     Flattening an enumerable out to a stream of individual elements
        ///     The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> FromEnumerable<TElem>(
            Func<Task<IEnumerable<TElem>>> func)
            => Source
                .Single(NotUsed.Instance)
                .SelectAsync(1, _ => func())
                .SelectMany(r => r);

        /// <summary>
        ///     Creates a Source using an Async function's result as input,
        ///     Flattening an enumerable out to a stream of individual elements
        ///     The Async function is evaluated once per each materialization
        /// </summary>
        /// <param name="state">The input state passed to the async function</param>
        /// <param name="func">The Async function producing a value</param>
        public static Source<TElem, NotUsed> FromEnumerable<TState, TElem>(
            TState state,
            Func<TState, Task<IEnumerable<TElem>>> func)
            => Source
                .Single(state)
                .SelectAsync(1, func)
                .SelectMany(r => r);
    }
}
