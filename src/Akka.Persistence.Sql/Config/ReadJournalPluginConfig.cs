// -----------------------------------------------------------------------
//  <copyright file="ReadJournalPluginConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Config
{
    public class ReadJournalPluginConfig
    {
        public ReadJournalPluginConfig(Configuration.Config config)
        {
            TagSeparator = config.GetString("tag-separator", ";");
            Dao = config.GetString("dao", "Akka.Persistence.Sql.Journal.Dao.ByteArrayJournalDao, Akka.Persistence.Sql");

            var tagReadValue = config.GetString("tag-read-mode", "TagTable").ToLowerInvariant();
            if (!Enum.TryParse<TagReadMode>(tagReadValue, true, out var tagReadMode))
                tagReadMode = TagReadMode.TagTable;

            TagReadMode = tagReadMode;
        }

        public string Dao { get; }

        public string TagSeparator { get; }

        public TagReadMode TagReadMode { get; }
    }

    public enum TagReadMode
    {
        Csv,
        TagTable
    }
}
