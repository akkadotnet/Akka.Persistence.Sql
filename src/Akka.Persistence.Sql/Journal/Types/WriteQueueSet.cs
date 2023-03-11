using System.Collections.Immutable;
using System.Threading.Tasks;
using LanguageExt;

namespace Akka.Persistence.Sql.Journal.Types
{
    public sealed class WriteQueueSet
    {
        public WriteQueueSet(ImmutableList<TaskCompletionSource<NotUsed>> tcs, Seq<JournalRow> rows)
        {
            Tcs = tcs;
            Rows = rows;
        }

        public Seq<JournalRow> Rows { get; }

        public ImmutableList<TaskCompletionSource<NotUsed>> Tcs { get; }
    }
}
