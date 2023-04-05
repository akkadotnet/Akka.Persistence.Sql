// -----------------------------------------------------------------------
//  <copyright file="SqlSnapshotOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Text;
using Akka.Hosting;
using Akka.Persistence.Hosting;

namespace Akka.Persistence.Sql.Hosting
{
    public sealed class SqlSnapshotOptions: SnapshotOptions
    {
        private static readonly Configuration.Config Default = SqlPersistence.DefaultSnapshotConfiguration;
        
        public SqlSnapshotOptions() : this(true)
        {
        }
        
        public SqlSnapshotOptions(bool isDefaultPlugin, string identifier = "sql") : base(isDefaultPlugin)
        {
            Identifier = identifier;
            Serializer = null!;
            AutoInitialize = true;
        }
        
        /// <summary>
        ///     Connection string used for database access
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        ///     <para>
        ///         A string constant defining the database type to connect to, valid values are defined inside
        ///         <see cref="LinqToDB.ProviderName"/> static class.
        ///         Refer to the Members of <see cref="LinqToDB.ProviderName"/> for included providers.
        ///     </para>
        /// </summary>
        public string? ProviderName { get; set; }
        
        /// <summary>
        ///     <para>
        ///         The plugin identifier for this persistence plugin
        ///     </para>
        ///     <b>Default</b>: <c>"sql"</c>
        /// </summary>
        public override string Identifier { get; set; }
        
        /// <summary>
        ///     <para>
        ///         The database options to modify database table column mapping for snapshot.
        ///     </para>
        ///     <b>NOTE</b>: This is used primarily for backward compatibility,
        ///     you leave this empty for greenfield projects.
        /// </summary>
        public SnapshotDatabaseOptions? DatabaseOptions { get; set; }
        
        protected override Configuration.Config InternalDefaultConfig => Default;

        protected override StringBuilder Build(StringBuilder sb)
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
                throw new ArgumentNullException(nameof(ConnectionString), $"{nameof(ConnectionString)} can not be null or empty.");
            
            if(string.IsNullOrWhiteSpace(ProviderName))
                throw new ArgumentNullException(nameof(ProviderName), $"{nameof(ProviderName)} can not be null or empty.");
            
            sb.AppendLine($"connection-string = {ConnectionString.ToHocon()}");
            sb.AppendLine($"provider-name = {ProviderName.ToHocon()}");
            
            if (DatabaseOptions is { })
                sb.AppendLine($"table-mapping = {DatabaseOptions.Mapping.Name().ToHocon()}");
            
            DatabaseOptions?.Build(sb);
            
            base.Build(sb);
            return sb;
        }
    }
}
