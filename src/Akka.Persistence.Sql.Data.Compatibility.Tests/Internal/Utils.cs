// -----------------------------------------------------------------------
//  <copyright file="Utils.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Persistence.Journal;

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.Internal
{
    public static class Utils
    {
        // We're only doing 100 entities
        public const int MaxEntities = 100;
        private const int TaggedVariants = 3;
        public const int MessagesPerType = MaxEntities * TaggedVariants;

        public static readonly string[] Tags = { "Tag1", "Tag2", "Tag3", "Tag4" };

        public static object ToTagged<T>(this T msg, int value)
        {
            if (msg is null)
                throw new ArgumentNullException(nameof(msg));
        
            return (value % TaggedVariants) switch
            {
                0 => msg,
                1 => new Tagged(msg, new[] { Tags[0] }),
                _ => new Tagged(msg, new[] { Tags[0], Tags[1] }),
            };
        }

        public static string ToEntityId(this int msg)
            => ((msg / 3) % MaxEntities).ToString();
    }    
}

