// -----------------------------------------------------------------------
//  <copyright file="SqlJournalOptions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Data;
using System.Text;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Akka.Persistence.Sql.Config;

namespace Akka.Persistence.Sql.Hosting
{
    public sealed class SqlJournalOptions : JournalOptions
    {
        private static readonly Configuration.Config Default = SqlPersistence.DefaultJournalConfiguration;

        public SqlJournalOptions() : this(true) { }

        public SqlJournalOptions(bool isDefaultPlugin, string identifier = "sql") : base(isDefaultPlugin)
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
        ///         <see cref="LinqToDB.ProviderName" /> static class.
        ///         Refer to the Members of <see cref="LinqToDB.ProviderName" /> for included providers.
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
        ///         The SQL write journal is notifying the query side as soon as things
        ///         are persisted, but for efficiency reasons the query side retrieves the events
        ///         in batches that sometimes can be delayed up to the configured <see cref="QueryRefreshInterval" />.
        ///     </para>
        ///     <b>Default</b>: 3 seconds
        /// </summary>
        public TimeSpan? QueryRefreshInterval { get; set; }

        /// <summary>
        ///     <para>
        ///         The database options to modify database table column mapping for this journal.
        ///     </para>
        ///     <b>NOTE</b>: This is used primarily for backward compatibility,
        ///     you leave this empty for greenfield projects.
        /// </summary>
        public JournalDatabaseOptions? DatabaseOptions { get; set; }

        /// <summary>
        ///     <para>
        ///         Describe how tags are being stored inside the database. Setting this to
        ///         <see cref="TagMode.Csv" /> will store the tags as a comma delimited value
        ///         in a column named <c>tags</c> inside the event journal. Setting this to
        ///         <see cref="TagMode.TagTable" /> will store the tags inside a separate
        ///         tag table instead.
        ///     </para>
        ///     <b>NOTE</b>: This is used primarily for backward compatibility,
        ///     you leave this empty for greenfield projects.
        /// </summary>
        public TagMode? TagStorageMode { get; set; }
        
        /// <summary>
        ///     <para>
        ///         If true, journal_metadata is created and used for deletes
        ///         and max sequence number queries.
        ///     </para>
        ///     <b>NOTE</b>: This is used primarily for backward compatibility,
        ///     you leave this empty for greenfield projects.
        /// </summary>
        public bool? DeleteCompatibilityMode { get; set; }
        
        /// <summary>
        ///     <para>
        ///         The isolation level of all database read query.
        ///     </para>
        ///     <para>
        ///         Isolation level documentation can be read
        ///         <a href="https://learn.microsoft.com/en-us/dotnet/api/system.data.isolationlevel?#fields">here</a> 
        ///     </para>
        /// </summary>
        public IsolationLevel? ReadIsolationLevel { get; set; }
        
        /// <summary>
        ///     <para>
        ///         The isolation level of all database write query.
        ///     </para>
        ///     <para>
        ///         Isolation level documentation can be read
        ///         <a href="https://learn.microsoft.com/en-us/dotnet/api/system.data.isolationlevel?#fields">here</a> 
        ///     </para>
        /// </summary>
        public IsolationLevel? WriteIsolationLevel { get; set; }

        protected override Configuration.Config InternalDefaultConfig => Default;

        protected override StringBuilder Build(StringBuilder sb)
        {
            if (string.IsNullOrWhiteSpace(ConnectionString))
                throw new ArgumentNullException(nameof(ConnectionString), $"{nameof(ConnectionString)} can not be null or empty.");

            if (string.IsNullOrWhiteSpace(ProviderName))
                throw new ArgumentNullException(nameof(ProviderName), $"{nameof(ProviderName)} can not be null or empty.");

            sb.AppendLine($"connection-string = {ConnectionString.ToHocon()}");
            sb.AppendLine($"provider-name = {ProviderName.ToHocon()}");

            if (DeleteCompatibilityMode is { })
                sb.AppendLine($"delete-compatibility-mode = {DeleteCompatibilityMode.ToHocon()}");
                
            if (TagStorageMode is { })
                sb.AppendLine($"tag-write-mode = {TagStorageMode.ToString().ToHocon()}");

            if (DatabaseOptions is { })
                sb.AppendLine($"table-mapping = {DatabaseOptions.Mapping.Name().ToHocon()}");

            if (ReadIsolationLevel is { })
                sb.AppendLine($"read-isolation-level = {ReadIsolationLevel.ToHocon()}");

            if (WriteIsolationLevel is { })
                sb.AppendLine($"write-isolation-level = {WriteIsolationLevel.ToHocon()}");

            DatabaseOptions?.Build(sb);

            base.Build(sb);

            if (IsDefaultPlugin)
            {
                sb.AppendLine("akka.persistence.query.journal.sql {");
                sb.AppendLine($"connection-string = {ConnectionString.ToHocon()}");
                sb.AppendLine($"provider-name = {ProviderName.ToHocon()}");

                if (TagStorageMode is { })
                    sb.AppendLine($"tag-read-mode = {TagStorageMode.ToString().ToHocon()}");

                if (ReadIsolationLevel is { })
                    sb.AppendLine($"read-isolation-level = {ReadIsolationLevel.ToHocon()}");
                
                sb.AppendLine("}");
            }

            if (QueryRefreshInterval is { })
                sb.AppendLine($"akka.persistence.query.journal.sql.refresh-interval = {QueryRefreshInterval.ToHocon()}");

            return sb;
        }
    }
}
