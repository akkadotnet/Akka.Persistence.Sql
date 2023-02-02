// -----------------------------------------------------------------------
//  <copyright file="PostgreSqlSpecSettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.PostgreSql
{
    public sealed class PostgreSqlSpecSettings: TestSettings
    {
        public static readonly PostgreSqlSpecSettings Instance = new PostgreSqlSpecSettings();

        private PostgreSqlSpecSettings()
        {
        }
        
        public override string ProviderName => LinqToDB.ProviderName.PostgreSQL;
        
        public override string TableMapping => "postgresql";
    }
}