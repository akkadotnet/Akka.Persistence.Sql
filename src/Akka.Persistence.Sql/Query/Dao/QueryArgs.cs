// -----------------------------------------------------------------------
//  <copyright file="QueryArgs.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Persistence.Sql.Config;

namespace Akka.Persistence.Sql.Query.Dao
{
    internal readonly struct QueryArgs
    {
        public readonly long Offset;
        public readonly long MaxOffset;
        public readonly int Max;
        public readonly TagMode Mode;
        public readonly string Tag;
            
        public QueryArgs(long offset, long maxOffset, int max, string tag, TagMode tagMode)
        {
            Offset = offset;
            MaxOffset = maxOffset;
            Max = max;
            Tag = tag;
            Mode= tagMode;
        }

        public QueryArgs(long offset, long maxOffset, int max, TagMode tagMode) : this(offset, maxOffset, max, null!,tagMode)
        {
        }
    }
}
