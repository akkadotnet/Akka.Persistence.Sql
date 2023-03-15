// -----------------------------------------------------------------------
//  <copyright file="ConfigKeys.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql
{
    public class ConfigKeys
    {
        // TODO: Remove this config key,
        // This is a sort of 'pooling' type option for JVM SlickDb
        // That doesn't make sense in ADO/Linq2Db.
        public static readonly string useSharedDb = "use-shared-db";
    }
}
