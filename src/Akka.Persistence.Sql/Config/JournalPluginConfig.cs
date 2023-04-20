// -----------------------------------------------------------------------
//  <copyright file="JournalPluginConfig.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Sql.Config
{
    public class JournalPluginConfig : IPluginConfig
    {
        public JournalPluginConfig(Configuration.Config config)
        {
            TagSeparator = config.GetString("tag-separator", ";");
            Dao = config.GetString("dao", "Akka.Persistence.Sql.Journal.Dao.ByteArrayJournalDao, Akka.Persistence.Sql");

            var tagWriteValue = config.GetString("tag-write-mode", "TagTable").ToLowerInvariant();
            if (!Enum.TryParse(tagWriteValue, true, out TagMode tagWriteMode))
                tagWriteMode = TagMode.TagTable;

            TagMode = tagWriteMode;
        }

        public string TagSeparator { get; }

        public string Dao { get; }

        public TagMode TagMode { get; }
    }
}
