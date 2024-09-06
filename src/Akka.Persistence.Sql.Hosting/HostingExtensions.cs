﻿// -----------------------------------------------------------------------
//  <copyright file="HostingExtensions.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using Akka.Actor;
using Akka.Hosting;
using Akka.Persistence.Hosting;
using Akka.Persistence.Sql.Config;
using LinqToDB;

namespace Akka.Persistence.Sql.Hosting
{
    public static class HostingExtensions
    {
        /// <summary>
        ///     Adds Akka.Persistence.Sql support to this <see cref="ActorSystem" />.
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
        ///     A string constant defining the database type to connect to, valid values are defined inside
        ///     <see cref="LinqToDB.ProviderName" /> static class.
        ///     Refer to the Members of <see cref="LinqToDB.ProviderName" /> for included providers.
        /// </param>
        /// <param name="mode">
        ///     <para>
        ///         Determines which settings should be added by this method call.
        ///     </para>
        ///     <i>Default</i>: <see cref="PersistenceMode.Both" />
        /// </param>
        /// <param name="schemaName">
        ///     <para>
        ///         SQL schema name to table corresponding with persistent journal.
        ///     </para>
        ///     <b>Default</b>: <c>null</c>
        /// </param>
        /// <param name="journalBuilder">
        ///     <para>
        ///         An <see cref="Action{T}" /> used to configure an <see cref="AkkaPersistenceJournalBuilder" /> instance.
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
        ///         A <c>bool</c> flag to set the plugin as the default persistence plugin for the <see cref="ActorSystem" />
        ///     </para>
        ///     <b>Default</b>: <c>true</c>
        /// </param>
        /// <param name="databaseMapping">
        ///     <para>
        ///         The <see cref="DatabaseMapping" /> to modify database table column mapping for this journal.
        ///     </para>
        ///     <b>NOTE</b>: This is used primarily for backward compatibility,
        ///     you leave this empty for greenfield projects.
        /// </param>
        /// <param name="tagStorageMode">
        ///     <para>
        ///         Describe how tags are being stored inside the database. Setting this to
        ///         <see cref="TagMode.Csv" /> will store the tags as a comma delimited value
        ///         in a column named <c>tags</c> inside the event journal. Setting this to
        ///         <see cref="TagMode.TagTable" /> will store the tags inside a separate
        ///         tag table instead.
        ///     </para>
        ///     <b>NOTE</b>: This is used primarily for backward compatibility,
        ///     you leave this empty for greenfield projects.
        /// </param>
        /// <param name="deleteCompatibilityMode">
        ///     <para>
        ///         If true, journal_metadata is created and used for deletes
        ///         and max sequence number queries.
        ///     </para>
        ///     <b>NOTE</b>: This is used primarily for backward compatibility,
        ///     you leave this empty for greenfield projects.
        /// </param>
        /// <param name="useWriterUuidColumn">
        ///     <para>
        ///         A flag to indicate if the writer_uuid column should be generated and be populated in run-time.
        ///     </para>
        ///     <b>Notes:</b>
        ///     <list type="number">
        ///         <item>
        ///             The column will only be generated if auto-initialize is set to true.
        ///         </item>
        ///         <item>
        ///             This feature is Akka.Persistence.Sql specific, setting this to true will break
        ///             backward compatibility with databases generated by other Akka.Persistence plugins.
        ///         </item>
        ///         <item>
        ///             <para>
        ///                 To make this feature work with legacy plugins, you will have to alter the old
        ///                 journal table:
        ///             </para>
        ///             <c>ALTER TABLE [journal_table_name] ADD [writer_uuid_column_name] VARCHAR(128);</c>
        ///         </item>
        ///         <item>
        ///             If set to true, the code will not check for backward compatibility. It will expect
        ///             that the `writer-uuid` column to be present inside the journal table.
        ///         </item>
        ///     </list>
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder" /> instance originally passed in.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <paramref name="journalBuilder" /> is set and <paramref name="mode" /> is set to
        ///     <see cref="PersistenceMode.SnapshotStore" />
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when <paramref name="connectionString"/> or <paramref name="providerName"/> is null
        ///     or whitespace
        /// </exception>
        public static AkkaConfigurationBuilder WithSqlPersistence(
            this AkkaConfigurationBuilder builder,
            string connectionString,
            string providerName,
            PersistenceMode mode = PersistenceMode.Both,
            string? schemaName = null,
            Action<AkkaPersistenceJournalBuilder>? journalBuilder = null,
            bool autoInitialize = true,
            string pluginIdentifier = "sql",
            bool isDefaultPlugin = true,
            DatabaseMapping? databaseMapping = null,
            TagMode? tagStorageMode = null,
            bool? deleteCompatibilityMode = null,
            bool? useWriterUuidColumn = null)
        {
            if (mode == PersistenceMode.SnapshotStore && journalBuilder is not null)
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
                TagStorageMode = tagStorageMode,
                DeleteCompatibilityMode = deleteCompatibilityMode,
            };

            if (databaseMapping is not null)
                journalOpt.DatabaseOptions = databaseMapping.Value.JournalOption();

            if (schemaName is not null)
            {
                journalOpt.DatabaseOptions ??= JournalDatabaseOptions.Default;
                journalOpt.DatabaseOptions.SchemaName = schemaName;
            }

            if (useWriterUuidColumn is not null)
            {
                journalOpt.DatabaseOptions ??= JournalDatabaseOptions.Default;
                journalOpt.DatabaseOptions.JournalTable ??= JournalTableOptions.Default;
                journalOpt.DatabaseOptions.JournalTable.UseWriterUuidColumn = useWriterUuidColumn;
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

            if (databaseMapping is not null)
                snapshotOpt.DatabaseOptions = databaseMapping.Value.SnapshotOption();

            if (schemaName is not null)
            {
                snapshotOpt.DatabaseOptions ??= new SnapshotDatabaseOptions(DatabaseMapping.Default);
                snapshotOpt.DatabaseOptions.SchemaName = schemaName;
            }

            return mode switch
            {
                PersistenceMode.Journal => builder.WithSqlPersistence(journalOpt, null),
                PersistenceMode.SnapshotStore => builder.WithSqlPersistence(null, snapshotOpt),
                PersistenceMode.Both => builder.WithSqlPersistence(journalOpt, snapshotOpt),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid PersistenceMode defined."),
            };
        }

        /// <summary>
        ///     Adds Akka.Persistence.Sql support to this <see cref="ActorSystem" />.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="dataOptions">
        ///     <para>
        ///         The custom <see cref="DataOptions"/> used for the connection to the database. If not provided,
        ///         <see cref="DataOptionsExtensions.UseConnectionString(LinqToDB.DataOptions,string,string)"/> will be used.
        ///     </para>
        ///     <para>
        ///         If provided, you must give enough information for linq2db to connect to the database.
        ///         This includes setting the connection string and provider name again, if needed in your use case.
        ///     </para>
        ///     <para>
        ///         The following settings will be always overriden by Akka.Persistence.Sql:
        ///         <list type="number">
        ///             <item>
        ///                 MappingSchema
        ///             </item>
        ///         </list>
        ///     </para>
        ///     <para>
        ///         DataOptions documentation can be read
        ///         <a href="https://linq2db.github.io/api/linq2db/LinqToDB.DataOptions.html">here</a>
        ///     </para>
        /// </param>
        /// <param name="autoInitialize">
        ///     <para>
        ///         Should the SQL store table be initialized automatically.
        ///     </para>
        ///     <i>Default</i>: <c>false</c>
        /// </param>
        /// <param name="mode">
        ///     <para>
        ///         Determines which settings should be added by this method call.
        ///     </para>
        ///     <i>Default</i>: <see cref="PersistenceMode.Both" />
        /// </param>
        /// <param name="schemaName">
        ///     <para>
        ///         SQL schema name to table corresponding with persistent journal.
        ///     </para>
        ///     <b>Default</b>: <c>null</c>
        /// </param>
        /// <param name="journalBuilder">
        ///     <para>
        ///         An <see cref="Action{T}" /> used to configure an <see cref="AkkaPersistenceJournalBuilder" /> instance.
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
        ///         A <c>bool</c> flag to set the plugin as the default persistence plugin for the <see cref="ActorSystem" />
        ///     </para>
        ///     <b>Default</b>: <c>true</c>
        /// </param>
        /// <param name="databaseMapping">
        ///     <para>
        ///         The <see cref="DatabaseMapping" /> to modify database table column mapping for this journal.
        ///     </para>
        ///     <b>NOTE</b>: This is used primarily for backward compatibility,
        ///     you leave this empty for greenfield projects.
        /// </param>
        /// <param name="tagStorageMode">
        ///     <para>
        ///         Describe how tags are being stored inside the database. Setting this to
        ///         <see cref="TagMode.Csv" /> will store the tags as a comma delimited value
        ///         in a column named <c>tags</c> inside the event journal. Setting this to
        ///         <see cref="TagMode.TagTable" /> will store the tags inside a separate
        ///         tag table instead.
        ///     </para>
        ///     <b>NOTE</b>: This is used primarily for backward compatibility,
        ///     you leave this empty for greenfield projects.
        /// </param>
        /// <param name="deleteCompatibilityMode">
        ///     <para>
        ///         If true, journal_metadata is created and used for deletes
        ///         and max sequence number queries.
        ///     </para>
        ///     <b>NOTE</b>: This is used primarily for backward compatibility,
        ///     you leave this empty for greenfield projects.
        /// </param>
        /// <param name="useWriterUuidColumn">
        ///     <para>
        ///         A flag to indicate if the writer_uuid column should be generated and be populated in run-time.
        ///     </para>
        ///     <b>Notes:</b>
        ///     <list type="number">
        ///         <item>
        ///             The column will only be generated if auto-initialize is set to true.
        ///         </item>
        ///         <item>
        ///             This feature is Akka.Persistence.Sql specific, setting this to true will break
        ///             backward compatibility with databases generated by other Akka.Persistence plugins.
        ///         </item>
        ///         <item>
        ///             <para>
        ///                 To make this feature work with legacy plugins, you will have to alter the old
        ///                 journal table:
        ///             </para>
        ///             <c>ALTER TABLE [journal_table_name] ADD [writer_uuid_column_name] VARCHAR(128);</c>
        ///         </item>
        ///         <item>
        ///             If set to true, the code will not check for backward compatibility. It will expect
        ///             that the `writer-uuid` column to be present inside the journal table.
        ///         </item>
        ///     </list>
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder" /> instance originally passed in.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <paramref name="journalBuilder" /> is set and <paramref name="mode" /> is set to
        ///     <see cref="PersistenceMode.SnapshotStore" />
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when <paramref name="dataOptions"/> is null 
        /// </exception>
        public static AkkaConfigurationBuilder WithSqlPersistence(
            this AkkaConfigurationBuilder builder,
            DataOptions dataOptions,
            PersistenceMode mode = PersistenceMode.Both,
            string? schemaName = null,
            Action<AkkaPersistenceJournalBuilder>? journalBuilder = null,
            bool autoInitialize = true,
            string pluginIdentifier = "sql",
            bool isDefaultPlugin = true,
            DatabaseMapping? databaseMapping = null,
            TagMode? tagStorageMode = null,
            bool? deleteCompatibilityMode = null,
            bool? useWriterUuidColumn = null)
        {
            if (mode == PersistenceMode.SnapshotStore && journalBuilder is not null)
                throw new Exception($"{nameof(journalBuilder)} can only be set when {nameof(mode)} is set to either {PersistenceMode.Both} or {PersistenceMode.Journal}");

            if (dataOptions is null)
                throw new ArgumentNullException(nameof(dataOptions), $"{nameof(dataOptions)} can not be null");
            
            var journalOpt = new SqlJournalOptions(isDefaultPlugin, pluginIdentifier)
            {
                AutoInitialize = autoInitialize,
                TagStorageMode = tagStorageMode,
                DeleteCompatibilityMode = deleteCompatibilityMode,
                DataOptions = dataOptions,
            };

            if (databaseMapping is not null)
                journalOpt.DatabaseOptions = databaseMapping.Value.JournalOption();

            if (schemaName is not null)
            {
                journalOpt.DatabaseOptions ??= JournalDatabaseOptions.Default;
                journalOpt.DatabaseOptions.SchemaName = schemaName;
            }

            if (useWriterUuidColumn is not null)
            {
                journalOpt.DatabaseOptions ??= JournalDatabaseOptions.Default;
                journalOpt.DatabaseOptions.JournalTable ??= JournalTableOptions.Default;
                journalOpt.DatabaseOptions.JournalTable.UseWriterUuidColumn = useWriterUuidColumn;
            }

            var adapters = new AkkaPersistenceJournalBuilder(journalOpt.Identifier, builder);
            journalBuilder?.Invoke(adapters);
            journalOpt.Adapters = adapters;

            var snapshotOpt = new SqlSnapshotOptions(isDefaultPlugin, pluginIdentifier)
            {
                DataOptions = dataOptions,
                AutoInitialize = autoInitialize,
            };

            if (databaseMapping is not null)
                snapshotOpt.DatabaseOptions = databaseMapping.Value.SnapshotOption();

            if (schemaName is not null)
            {
                snapshotOpt.DatabaseOptions ??= new SnapshotDatabaseOptions(DatabaseMapping.Default);
                snapshotOpt.DatabaseOptions.SchemaName = schemaName;
            }

            return mode switch
            {
                PersistenceMode.Journal => builder.WithSqlPersistence(journalOpt, null),
                PersistenceMode.SnapshotStore => builder.WithSqlPersistence(null, snapshotOpt),
                PersistenceMode.Both => builder.WithSqlPersistence(journalOpt, snapshotOpt),
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Invalid PersistenceMode defined."),
            };
        }
        
        /// <summary>
        ///     Adds Akka.Persistence.Sql support to this <see cref="ActorSystem" />. At least one of the
        ///     configurator delegate needs to be populated else this method will throw an exception.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="journalOptionConfigurator">
        ///     <para>
        ///         An <see cref="Action{T}" /> that modifies an instance of <see cref="SqlJournalOptions" />,
        ///         used to configure the journal plugin
        ///     </para>
        ///     <i>Default</i>: <c>null</c>
        /// </param>
        /// <param name="snapshotOptionConfigurator">
        ///     <para>
        ///         An <see cref="Action{T}" /> that modifies an instance of <see cref="SqlSnapshotOptions" />,
        ///         used to configure the snapshot store plugin
        ///     </para>
        ///     <i>Default</i>: <c>null</c>
        /// </param>
        /// <param name="isDefaultPlugin">
        ///     <para>
        ///         A <c>bool</c> flag to set the plugin as the default persistence plugin for the <see cref="ActorSystem" />
        ///     </para>
        ///     <b>Default</b>: <c>true</c>
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder" /> instance originally passed in.
        /// </returns>
        /// <exception cref="ArgumentException">
        ///     Thrown when both <paramref name="journalOptionConfigurator" /> and <paramref name="snapshotOptionConfigurator" />
        ///     are null.
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
            if (journalOptionConfigurator is not null)
            {
                journalOptions = new SqlJournalOptions(isDefaultPlugin);
                journalOptionConfigurator(journalOptions);
            }

            SqlSnapshotOptions? snapshotOptions = null;
            if (snapshotOptionConfigurator is not null)
            {
                snapshotOptions = new SqlSnapshotOptions(isDefaultPlugin);
                snapshotOptionConfigurator(snapshotOptions);
            }

            return builder.WithSqlPersistence(journalOptions, snapshotOptions);
        }

        /// <summary>
        ///     Adds Akka.Persistence.Sql support to this <see cref="ActorSystem" />. At least one of the options
        ///     have to be populated else this method will throw an exception.
        /// </summary>
        /// <param name="builder">
        ///     The builder instance being configured.
        /// </param>
        /// <param name="journalOptions">
        ///     <para>
        ///         An instance of <see cref="SqlJournalOptions" />, used to configure the journal plugin
        ///     </para>
        ///     <i>Default</i>: <c>null</c>
        /// </param>
        /// <param name="snapshotOptions">
        ///     <para>
        ///         An instance of <see cref="SqlSnapshotOptions" />, used to configure the snapshot store plugin
        ///     </para>
        ///     <i>Default</i>: <c>null</c>
        /// </param>
        /// <returns>
        ///     The same <see cref="AkkaConfigurationBuilder" /> instance originally passed in.
        /// </returns>
        /// <exception cref="ArgumentException">
        ///     Thrown when both <paramref name="journalOptions" /> and <paramref name="snapshotOptions" /> are null.
        /// </exception>
        public static AkkaConfigurationBuilder WithSqlPersistence(
            this AkkaConfigurationBuilder builder,
            SqlJournalOptions? journalOptions = null,
            SqlSnapshotOptions? snapshotOptions = null)
        {
            if (journalOptions?.DataOptions is not null)
            {
                if(builder.Setups.First(s => s is MultiDataOptionsSetup) is not MultiDataOptionsSetup setup)
                {
                    setup = new MultiDataOptionsSetup();
                    builder.Setups.Add(setup);
                }
                setup.AddDataOptions(journalOptions.PluginId, journalOptions.DataOptions);
                setup.AddDataOptions(journalOptions.QueryPluginId, journalOptions.DataOptions);
            }

            if (snapshotOptions?.DataOptions is not null)
            {
                if(builder.Setups.First(s => s is MultiDataOptionsSetup) is not MultiDataOptionsSetup setup)
                {
                    setup = new MultiDataOptionsSetup();
                    builder.Setups.Add(setup);
                }
                setup.AddDataOptions(snapshotOptions.PluginId, snapshotOptions.DataOptions);
            }
            
            return (journalOptions, snapshotOptions) switch
            {
                (null, null) =>
                    throw new ArgumentException($"{nameof(journalOptions)} and {nameof(snapshotOptions)} could not both be null"),

                (_, null) =>
                    builder
                        .AddHocon(journalOptions.ToConfig(), HoconAddMode.Prepend)
                        .AddHocon(journalOptions.DefaultConfig, HoconAddMode.Append)
                        .AddHocon(journalOptions.DefaultQueryConfig, HoconAddMode.Append),

                (null, _) =>
                    builder
                        .AddHocon(snapshotOptions.ToConfig(), HoconAddMode.Prepend)
                        .AddHocon(snapshotOptions.DefaultConfig, HoconAddMode.Append),

                (_, _) =>
                    builder
                        .AddHocon(journalOptions.ToConfig(), HoconAddMode.Prepend)
                        .AddHocon(snapshotOptions.ToConfig(), HoconAddMode.Prepend)
                        .AddHocon(journalOptions.DefaultConfig, HoconAddMode.Append)
                        .AddHocon(snapshotOptions.DefaultConfig, HoconAddMode.Append)
                        .AddHocon(journalOptions.DefaultQueryConfig, HoconAddMode.Append),
            };
        }
    }
}
