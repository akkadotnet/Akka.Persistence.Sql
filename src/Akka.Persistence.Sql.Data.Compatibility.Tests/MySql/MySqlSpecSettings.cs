// -----------------------------------------------------------------------
//  <copyright file="MySqlSpecSettings.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2022 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Linq2Db.Data.Compatibility.Tests.MySql
{
    public sealed class MySqlSpecSettings : TestSettings
    {
        public static readonly MySqlSpecSettings Instance = new MySqlSpecSettings();

        private MySqlSpecSettings()
        {
        }
        
        public override string ProviderName => LinqToDB.ProviderName.MySql;
        
        public override string TableMapping => "mysql";

    }
}