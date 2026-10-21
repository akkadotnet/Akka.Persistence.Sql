// -----------------------------------------------------------------------
//  <copyright file="TagTableQueryableInsertMode.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Sql.Config
{
    /// <summary>
    /// Controls how the tag-table fast-path insert uses LinqToDB's <c>AsQueryable()</c>
    /// when writing tagged events. Only effective when <c>tag-write-mode</c> is
    /// <see cref="TagMode.TagTable"/> and the provider is SQL Server, PostgreSQL, or SQLite.
    /// UwU~ pick your insert flavour! ✨
    /// </summary>
    public enum TagTableQueryableInsertMode
    {
        /// <summary>
        /// Fast-path is disabled. Falls back to the standard BulkCopy / row-by-row path.
        /// Equivalent to the old <c>use-tagtable-asqueryable-literal-insert = false</c>.
        /// </summary>
        Off,

        /// <summary>
        /// Uses <c>AsQueryable().Parameterize()</c> — all inserted values are sent as SQL
        /// parameters. Safest option; compatible with all row/payload sizes.
        /// Great choice when messages are large or the DB has tight SQL length limits. (≧◡≦)
        /// </summary>
        Parameterized,

        /// <summary>
        /// Uses <c>AsQueryable().Inline().Except(Message, Manifest, WriterUuid, PersistenceId)</c> —
        /// scalar values (sequence number, timestamp, identifier) are emitted as SQL literals
        /// for maximum throughput, while byte-heavy columns remain parameterized via <c>Except</c>.
        /// Equivalent to the old <c>use-tagtable-asqueryable-literal-insert = true</c>. ✨
        /// </summary>
        Inline,
    }
}

