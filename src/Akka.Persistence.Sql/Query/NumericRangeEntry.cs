using System;
using System.Collections;
using System.Collections.Generic;

namespace Akka.Persistence.Sql.Linq2Db.Query
{
    /// <summary>
    /// A Class used to store ranges of numbers held
    /// more efficiently than full arrays.
    /// </summary>
    public class NumericRangeEntry: IEnumerable<long>
    {
        public NumericRangeEntry(long from, long until)
        {
            From = from;
            Until = until;
        }
        
        public long From { get; }
        public long Until { get;}

        public bool InRange(long number)
        {
            return  From <= number && number <= Until;
        }

        public IEnumerable<long> ToEnumerable()
        {
            for (var i = From; i < Until; i++)
            {
                yield return i;
            }
        }

        public IEnumerator<long> GetEnumerator()
        {
            return ToEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}