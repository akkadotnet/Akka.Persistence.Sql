// -----------------------------------------------------------------------
//  <copyright file="ExtSeq.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System.Threading.Tasks;
using Akka.Streams.Dsl;
using LanguageExt;

namespace Akka.Persistence.Sql.Streams
{
    public static class ExtSeq
    {
        public static Sink<TIn, Task<Seq<TIn>>> Seq<TIn>() => Sink.FromGraph(new ExtSeqStage<TIn>());
    }
}
