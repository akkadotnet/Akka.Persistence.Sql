using System;
using System.Collections;
using System.Collections.Generic;
using Akka.Streams.Dsl;

namespace Akka.Persistence.Sql.Linq2Db.Query.Dao
{
    public static class FactoryEnumerable
    {
        /// <summary>
        /// Generates a Wrapped Enumerable, such that the factory is not executed
        /// until materialization.
        /// </summary>
        /// <param name="factory"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static Source<T, NotUsed> StreamSource<T>(Func<IEnumerable<T>> factory)
        {
            return Source.From(new FactoryEnumerable<T>(factory));
        }
    }
    /// <summary>
    /// A Helper class that is used to set up Persistence queries to be 'semi-lazy'.
    /// Basically, so that we don't actually run the query until we materialize,
    /// But we still will run it in one go.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class FactoryEnumerable<T> : IEnumerable<T>
    {
        private readonly Func<IEnumerable<T>> _enumeratorFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="FactoryEnumerable{T}"/> class.
        /// </summary>
        /// <param name="enumeratorFactory">The method used to create an <see cref="IEnumerable{T}"/> that is wrapped.</param>
        public FactoryEnumerable(Func<IEnumerable<T>> enumeratorFactory)
        {
            _enumeratorFactory = enumeratorFactory;
        }

        /// <inheritdoc/>
        public IEnumerator<T> GetEnumerator() => _enumeratorFactory().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}