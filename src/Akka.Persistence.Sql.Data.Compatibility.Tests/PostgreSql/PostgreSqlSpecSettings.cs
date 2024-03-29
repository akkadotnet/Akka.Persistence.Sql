﻿// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlSpecSettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.PostgreSql
{
    public sealed class PostgreSqlSpecSettings : TestSettings
    {
        public static readonly PostgreSqlSpecSettings Instance = new();

        private PostgreSqlSpecSettings() { }

        public override string ProviderName => LinqToDB.ProviderName.PostgreSQL;

        public override string TableMapping => "postgresql";
    }
}
