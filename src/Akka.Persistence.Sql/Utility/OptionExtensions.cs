// -----------------------------------------------------------------------
//  <copyright file="OptionExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using Akka.Util;

namespace Akka.Persistence.Sql.Utility
{
    public static class OptionExtensions
    {
        public static T? GetOrNull<T>(this Option<T> opt) => opt.HasValue ? opt.Value : default;
    }
}
