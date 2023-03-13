using System;
using System.Collections.Generic;
using System.Linq;
using LanguageExt;

namespace Akka.Persistence.Sql.Utility
{
    public static class TrySeq
    {
        public static Util.Try<IEnumerable<T>> Sequence<T>(IEnumerable<Util.Try<T>> seq)
        {
            return Util.Try<IEnumerable<T>>.From(()=>seq.Select(r => r.Get()));
        }
        public static Util.Try<List<T>> SequenceList<T>(IEnumerable<Util.Try<T>> seq)
        {
            try
            {
                return new Util.Try<List<T>>(seq.Select(r => r.Get()).ToList());
            }
            catch (Exception e)
            {
                return new Util.Try<List<T>>(e);
            }
        }
        public static Util.Try<Seq<T>> SequenceSeq<T>(IEnumerable<Util.Try<T>> seq)
        {
            return Util.Try<Seq<T>>.From(()=>seq.Select(r => r.Get()).ToList().ToSeq());
        }
    }
}
