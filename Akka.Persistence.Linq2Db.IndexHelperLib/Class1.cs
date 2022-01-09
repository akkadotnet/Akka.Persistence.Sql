using System;
using FluentMigrator.Model;

namespace Akka.Persistence.Linq2Db.IndexHelperLib
{
    public class JournalIndexHelper
    {
        public IndexDefinition DefaultJournalIndex(string tableName, string persistenceIdCol, string sequenceNoCol, string schemaName = null)
        {
            var idx =  beginCreateIndex(tableName, schemaName, $"UX_{tableName}_PID_SEQNO");
            //short name for easy compat with all dbs. (*cough* oracle *cough*)
            idx.Columns.Add(new IndexColumnDefinition(){ Name = persistenceIdCol });
            idx.Columns.Add(new IndexColumnDefinition(){Name = sequenceNoCol, Direction = Direction.Ascending});
            idx.IsUnique = true;
            return idx;
        }

        public IndexDefinition JournalOrdering(string tableName,
            string orderingCol, string schemaName = null)
        {
            var idx =  beginCreateIndex(tableName, schemaName,$"IX_{tableName}_Ordering");
            idx.Columns.Add(new IndexColumnDefinition(){Name = orderingCol});
            //Should it be?
            //idx.IsUnique = true;
            return idx;
        }

        public IndexDefinition JournalTimestamp(string tableName,
            string timestampCol, string schemaName = null)
        {
            var idx = beginCreateIndex(tableName, schemaName,
                $"IX_{tableName}_TimeStamp");
            idx.Columns.Add(new IndexColumnDefinition(){Name = timestampCol});
            //Not unique by any stretch.
            return idx;
        }

        private static IndexDefinition beginCreateIndex(string tableName, string schemaName, string indexName)
        {
            var idx = new IndexDefinition();
            if (string.IsNullOrWhiteSpace(schemaName) == false)
            {
                idx.SchemaName = schemaName;
            }
            idx.TableName = tableName;
            idx.Name = indexName;
            return idx;
        }
    }
}