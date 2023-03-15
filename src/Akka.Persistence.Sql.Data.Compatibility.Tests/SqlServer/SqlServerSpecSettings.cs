// -----------------------------------------------------------------------
//  <copyright file="SqlServerSpecSettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Data.Compatibility.Tests.SqlServer
{
    public sealed class SqlServerSpecSettings : TestSettings
    {
        public static readonly SqlServerSpecSettings Instance = new();

        private SqlServerSpecSettings() { }

        public override string ProviderName => LinqToDB.ProviderName.SqlServer2017;

        public override string TableMapping => "sql-server";
    }
}
