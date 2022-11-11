using System.Collections.Generic;
using System.Threading.Tasks;
using LanguageExt;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Types
{
    public sealed class WriteQueueSet
    {
        public WriteQueueSet(List<TaskCompletionSource<NotUsed>> tcs, Seq<JournalRow> rows)
        {
            Tcs = tcs;
            Rows = rows;
        }

        public Seq<JournalRow> Rows { get; set; }

        public List<TaskCompletionSource<NotUsed>> Tcs { get; }
    }
}