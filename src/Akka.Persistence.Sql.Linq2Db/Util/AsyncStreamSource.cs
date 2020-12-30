using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Streams.Dsl;
using Akka.Util;

namespace Akka.Persistence.Sql.Linq2Db.Utility
{
    public static class AsyncStreamSource
    {
        public enum StreamState
        {
            unread,
            read
        }
        public static Source<TElem, NotUsed> From<TElem>(
            Func<Task<TElem>> func)
        {
            return Source.UnfoldAsync(StreamState.unread, async (state) =>
            {
                if (state == StreamState.unread)
                {
                    return new Option<(StreamState, TElem)>((StreamState.read,
                        await func()));
                }
                else
                {
                    return Option<(StreamState, TElem)>.None;
                }
            });
        }
        public static Source<TElem, NotUsed> FromEnumerable<TElem>(
            Func<Task<IEnumerable<TElem>>> func)
        {
            return Source.UnfoldAsync(StreamState.unread, async (state) =>
            {
                if (state == StreamState.unread)
                {
                    return new Option<(StreamState, IEnumerable<TElem>)>((StreamState.read,
                        await func()));
                }
                else
                {
                    return Option<(StreamState, IEnumerable<TElem>)>.None;
                }
            }).SelectMany(r=>r);
        }
    }
}