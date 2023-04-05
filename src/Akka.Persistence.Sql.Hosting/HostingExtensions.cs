// -----------------------------------------------------------------------
//  <copyright file="HostingExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Hosting;

namespace Akka.Persistence.Sql.Hosting
{
    public static class HostingExtensions
    {
        /// <summary>
        ///     Adds Akka.Persistence.SqlServer support to this <see cref="ActorSystem"/>.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="connectionString">
        ///     Connection string used for database access.
        /// </param>
        /// <param name="autoInitialize">
        ///     <para>
        ///         Should the SQL store table be initialized automatically.
        ///     </para>
        ///     <i>Default</i>: <c>false</c>
        /// </param>
        /// <param name="providerName">
        ///     <para>
        ///         A string constant defining the database type to connect to, valid values are defined inside
        ///         <see cref="LinqToDB.ProviderName"/> static class.
        ///         Refer to the Members of <see cref="LinqToDB.ProviderName"/> for included providers.
        ///     </para>
        /// </param>
        /// <param name="mode">
        ///     <para>
        ///         Determines which settings should be added by this method call.
        ///     </para>
        ///     <i>Default</i>: <see cref="PersistenceMode.Both"/>
        /// </param>
        /// <param name="schemaName">
        ///     <para>
        ///         SQL schema name to table corresponding with persistent journal.
        ///     </para>
        ///     <b>Default</b>: <c>null</c>
        /// </param>
        /// <param name="journalBuilder">
        ///     <para>
        ///         An <see cref="Action{T}"/> used to configure an <see cref="AkkaPersistenceJournalBuilder"/> instance.
        ///     </para>
        ///     <i>Default</i>: <c>null</c>
        /// </param>
        /// <param name="pluginIdentifier">
        ///     <para>
        ///         The configuration identifier for the plugins
        ///     </para>
        ///     <i>Default</i>: <c>"sql"</c>
        /// </param>
        /// <param name="isDefaultPlugin">
        ///     <para>
        ///         A <c>bool</c> flag to set the plugin as the default persistence plugin for the <see cref="ActorSystem"/>
        ///     </para>
        ///     <b>Default</b>: <c>true</c>
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <see cref="journalBuilder"/> is set and <see cref="mode"/> is set to
        ///     <see cref="PersistenceMode.SnapshotStore"/>
        /// </exception>
        public static AkkaConfigurationBuilder WithSqlServerPersistence(
            this AkkaConfigurationBuilder builder,
            string connectionString,
            string providerName,
            PersistenceMode mode = PersistenceMode.Both, 
            string? schemaName = null, 
            Action<AkkaPersistenceJournalBuilder>? journalBuilder = null,
            bool autoInitialize = true,
            string pluginIdentifier = "sql",
            bool isDefaultPlugin = true)
        {
            if (mode == PersistenceMode.SnapshotStore && journalBuilder is { })
                throw new Exception($"{nameof(journalBuilder)} can only be set when {nameof(mode)} is set to either {PersistenceMode.Both} or {PersistenceMode.Journal}");

            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentNullException(nameof(connectionString), $"{nameof(connectionString)} can not be null");

            if (string.IsNullOrWhiteSpace(providerName))
                throw new ArgumentNullException(nameof(providerName), $"{nameof(providerName)} can not be null");
            
            var journalOpt = new SqlJournalOptions(isDefaultPlugin, pluginIdentifier)
            {
                ConnectionString = connectionString,
                ProviderName = providerName,
                AutoInitialize = autoInitialize,
            };

            if (schemaName is { })
            {
                journalOpt.DatabaseOptions = new JournalDatabaseOptions(DatabaseMapping.Default)
                {
                    SchemaName = schemaName
                };
            }

            var adapters = new AkkaPersistenceJournalBuilder(journalOpt.Identifier, builder);
            journalBuilder?.Invoke(adapters);
            journalOpt.Adapters = adapters;

            var snapshotOpt = new SqlSnapshotOptions(isDefaultPlugin, pluginIdentifier)
            {
                ConnectionString = connectionString,
                ProviderName = providerName,
                AutoInitialize = autoInitialize,
            };

            if (schemaName is { })
            {
                snapshotOpt.DatabaseOptions = new SnapshotDatabaseOptions(DatabaseMapping.Default)
                {
                    SchemaName = schemaName
                };
            }

            return mode switch
            {
                PersistenceMode.Journal => builder.WithSqlPersistence(journalOpt, null),
                PersistenceMode.SnapshotStore => builder.WithSqlPersistence(null, snapshotOpt),
                PersistenceMode.Both => builder.WithSqlPersistence(journalOpt, snapshotOpt),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid PersistenceMode defined.")
            };
        }

        /// <summary>
        ///     Adds Akka.Persistence.SqlServer support to this <see cref="ActorSystem"/>. At least one of the
        ///     configurator delegate needs to be populated else this method will throw an exception.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="journalOptionConfigurator">
        ///     <para>
        ///         An <see cref="Action{T}"/> that modifies an instance of <see cref="SqlJournalOptions"/>,
        ///         used to configure the journal plugin
        ///     </para>
        ///     <i>Default</i>: <c>null</c>
        /// </param>
        /// <param name="snapshotOptionConfigurator">
        ///     <para>
        ///         An <see cref="Action{T}"/> that modifies an instance of <see cref="SqlSnapshotOptions"/>,
        ///         used to configure the snapshot store plugin
        ///     </para>
        ///     <i>Default</i>: <c>null</c>
        /// </param>
        /// <param name="isDefaultPlugin">
        ///     <para>
        ///         A <c>bool</c> flag to set the plugin as the default persistence plugin for the <see cref="ActorSystem"/>
        ///     </para>
        ///     <b>Default</b>: <c>true</c>
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <exception cref="ArgumentException">
        ///     Thrown when both <paramref name="journalOptionConfigurator"/> and <paramref name="snapshotOptionConfigurator"/> are null.
        /// </exception>
        public static AkkaConfigurationBuilder WithSqlPersistence(
            this AkkaConfigurationBuilder builder,
            Action<SqlJournalOptions>? journalOptionConfigurator = null,
            Action<SqlSnapshotOptions>? snapshotOptionConfigurator = null,
            bool isDefaultPlugin = true)
        {
            if (journalOptionConfigurator is null && snapshotOptionConfigurator is null)
                throw new ArgumentException($"{nameof(journalOptionConfigurator)} and {nameof(snapshotOptionConfigurator)} could not both be null");
            
            SqlJournalOptions? journalOptions = null;
            if (journalOptionConfigurator is { })
            {
                journalOptions = new SqlJournalOptions(isDefaultPlugin);
                journalOptionConfigurator(journalOptions);
            }

            SqlSnapshotOptions? snapshotOptions = null;
            if (snapshotOptionConfigurator is { })
            {
                snapshotOptions = new SqlSnapshotOptions(isDefaultPlugin);
                snapshotOptionConfigurator(snapshotOptions);
            }

            return builder.WithSqlPersistence(journalOptions, snapshotOptions);
        }
        
        /// <summary>
        ///     Adds Akka.Persistence.SqlServer support to this <see cref="ActorSystem"/>. At least one of the options
        ///     have to be populated else this method will throw an exception.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="journalOptions">
        ///     <para>
        ///         An instance of <see cref="SqlJournalOptions"/>, used to configure the journal plugin
        ///     </para>
        ///     <i>Default</i>: <c>null</c>
        /// </param>
        /// <param name="snapshotOptions">
        ///     <para>
        ///         An instance of <see cref="SqlSnapshotOptions"/>, used to configure the snapshot store plugin
        ///     </para>
        ///     <i>Default</i>: <c>null</c>
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder"/> instance originally passed in.
        /// </returns>
        /// <exception cref="ArgumentException">
        ///     Thrown when both <paramref name="journalOptions"/> and <paramref name="snapshotOptions"/> are null.
        /// </exception>
        public static AkkaConfigurationBuilder WithSqlPersistence(
            this AkkaConfigurationBuilder builder,
            SqlJournalOptions? journalOptions = null,
            SqlSnapshotOptions? snapshotOptions = null)
        {
            return (journalOptions, snapshotOptions) switch
            {
                (null, null) => 
                    throw new ArgumentException($"{nameof(journalOptions)} and {nameof(snapshotOptions)} could not both be null"),
                
                (_, null) => 
                    builder
                        .AddHocon(journalOptions.ToConfig(), HoconAddMode.Prepend)
                        .AddHocon(journalOptions.DefaultConfig, HoconAddMode.Append),
                
                (null, _) => 
                    builder
                        .AddHocon(snapshotOptions.ToConfig(), HoconAddMode.Prepend)
                        .AddHocon(snapshotOptions.DefaultConfig, HoconAddMode.Append),
                
                (_, _) => 
                    builder
                        .AddHocon(journalOptions.ToConfig(), HoconAddMode.Prepend)
                        .AddHocon(snapshotOptions.ToConfig(), HoconAddMode.Prepend)
                        .AddHocon(journalOptions.DefaultConfig, HoconAddMode.Append)
                        .AddHocon(snapshotOptions.DefaultConfig, HoconAddMode.Append),
            };
        }

    }
}
