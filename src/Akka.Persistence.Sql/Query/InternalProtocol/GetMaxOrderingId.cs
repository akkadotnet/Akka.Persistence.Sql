// -----------------------------------------------------------------------
//  <copyright file="GetMaxOrderingId.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Query.InternalProtocol
{
    public class GetMaxOrderingId
    {
        public static readonly GetMaxOrderingId Instance = new();

        private GetMaxOrderingId() { }
    }
}
