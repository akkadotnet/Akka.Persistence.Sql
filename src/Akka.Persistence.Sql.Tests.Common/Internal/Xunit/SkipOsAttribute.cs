// -----------------------------------------------------------------------
//  <copyright file="SkipOsAttribute.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Tests.Common.Internal.Xunit
{
    [AttributeUsage(AttributeTargets.Class)]
    public class SkipLinuxAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public class SkipWindowsAttribute : Attribute { }
}
