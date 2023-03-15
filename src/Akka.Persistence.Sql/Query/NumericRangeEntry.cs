// -----------------------------------------------------------------------
//  <copyright file="NumericRangeEntry.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;

namespace Akka.Persistence.Sql.Query
{
    /// <summary>
    ///     A Class used to store ranges of numbers held
    ///     more efficiently than full arrays.
    /// </summary>
    public class NumericRangeEntry : IEnumerable<long>
    {
        public NumericRangeEntry(long from, long until)
        {
            From = from;
            Until = until;
        }

        public long From { get; }
        public long Until { get; }

        public IEnumerator<long> GetEnumerator()
            => ToEnumerable().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        public bool InRange(long number)
            => From <= number && number <= Until;

        public IEnumerable<long> ToEnumerable()
        {
            for (var i = From; i < Until; i++)
                yield return i;
        }
    }
}
