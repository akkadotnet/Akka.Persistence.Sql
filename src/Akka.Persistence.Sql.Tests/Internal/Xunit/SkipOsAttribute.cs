// -----------------------------------------------------------------------
//  <copyright file="LinuxFact.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Tests.Internal.Xunit
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class SkipLinuxAttribute: Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class SkipWindowsAttribute: Attribute
    {
    }
}
