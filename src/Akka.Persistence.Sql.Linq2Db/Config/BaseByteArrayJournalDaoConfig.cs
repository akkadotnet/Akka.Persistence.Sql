using LinqToDB.Data;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class BaseByteArrayJournalDaoConfig : IDaoConfig
    {
        public BaseByteArrayJournalDaoConfig(Configuration.Config config)
        {
            BufferSize = config.GetInt("buffer-size", 5000);
            BatchSize = config.GetInt("batch-size", 100);
            DbRoundTripBatchSize = config.GetInt("db-round-trip-max-batch-size", 1000);
            DbRoundTripTagBatchSize = config.GetInt("db-round-trip-max-tag-batch-size", 1000);
            PreferParametersOnMultiRowInsert = config.GetBoolean("prefer-parameters-on-multirow-insert", false);
            ReplayBatchSize = config.GetInt("replay-batch-size", 1000);
            Parallelism = config.GetInt("parallelism", 2);
            LogicalDelete = config.GetBoolean("logical-delete", false);
            MaxRowByRowSize = config.GetInt("max-row-by-row-size", 100);
            SqlCommonCompatibilityMode = config.GetBoolean("delete-compatibility-mode", true);
        }

        public bool PreferParametersOnMultiRowInsert { get; }

        public int DbRoundTripBatchSize { get; }

        /// <summary>
        /// Specifies the batch size at which point <see cref="BulkCopyType"/>
        /// will switch to 'Default' instead of 'MultipleRows'. For smaller sets
        /// (i.e. 100 entries or less) the cost of Bulk copy setup for DB may be worse.
        /// </summary>
        public int MaxRowByRowSize { get; }

        public int Parallelism { get; }

        public int BatchSize { get; }

        public bool LogicalDelete { get; }

        public int ReplayBatchSize { get; }

        public int BufferSize { get; }
        
        public bool SqlCommonCompatibilityMode { get; }
        public int DbRoundTripTagBatchSize { get; }
    }
}