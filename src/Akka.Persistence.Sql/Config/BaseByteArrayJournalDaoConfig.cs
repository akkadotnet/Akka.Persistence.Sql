// -----------------------------------------------------------------------
//  <copyright file="BaseByteArrayJournalDaoConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using LinqToDB.Data;

namespace Akka.Persistence.Sql.Config
{
    public class BaseByteArrayJournalDaoConfig : IDaoConfig
    {
        public BaseByteArrayJournalDaoConfig(Configuration.Config config)
        {
            BufferSize = config.GetInt("buffer-size", 5000);
            BatchSize = config.GetInt("batch-size", 100);
            DbRoundTripBatchSize = config.GetInt("db-round-trip-max-batch-size", 1000);
            DbRoundTripTagBatchSize = config.GetInt("db-round-trip-max-tag-batch-size", 1000);
            PreferParametersOnMultiRowInsert = config.GetBoolean("prefer-parameters-on-multirow-insert");
            ReplayBatchSize = config.GetInt("replay-batch-size", 1000);
            Parallelism = config.GetInt("parallelism", 2);
            MaxRowByRowSize = config.GetInt("max-row-by-row-size", 100);
            SqlCommonCompatibilityMode = config.GetBoolean("delete-compatibility-mode");
            AsQueryableInsertSqlLengthLimit = config.GetInt("tagtable-asqueryable-insert-sql-length-limit", 5_000_000);

            // CopilotNote: Parse new enum-style key first, then fall back to the legacy bool key
            // for backward compatibility (true → Inline, false → Off). UwU~
            var modeStr = config.GetString("tagtable-asqueryable-insert-mode", null);
            if (modeStr is not null && System.Enum.TryParse<TagTableQueryableInsertMode>(modeStr, true, out var parsedMode))
            {
                TagTableQueryableInsertMode = parsedMode;
            }
            else
            {
                // Backward compat: if the new key is absent, check the old bool key.
                // true → Inline (was the original "literal insert" intent), false → Off.
                TagTableQueryableInsertMode = config.GetBoolean("use-tagtable-asqueryable-literal-insert", false)
                    ? TagTableQueryableInsertMode.Inline
                    : TagTableQueryableInsertMode.Off;
            }
        }

        public bool PreferParametersOnMultiRowInsert { get; }

        public int DbRoundTripBatchSize { get; }

        /// <summary>
        ///     Specifies the batch size at which point <see cref="BulkCopyType" />
        ///     will switch to 'Default' instead of 'MultipleRows'. For smaller sets
        ///     (i.e. 100 entries or less) the cost of Bulk copy setup for DB may be worse.
        /// </summary>
        public int MaxRowByRowSize { get; }

        public int BatchSize { get; }

        public int ReplayBatchSize { get; }

        public int BufferSize { get; }

        public int DbRoundTripTagBatchSize { get; }

        public int Parallelism { get; }

        public bool SqlCommonCompatibilityMode { get; }
        
        public int AsQueryableInsertSqlLengthLimit { get; }
        
        /// <summary>
        /// Controls which LinqToDB <c>AsQueryable()</c> strategy is used for the tag-table
        /// fast-path insert. See <see cref="TagTableQueryableInsertMode"/> for options.
        /// </summary>
        public TagTableQueryableInsertMode TagTableQueryableInsertMode { get; }
    }
}
