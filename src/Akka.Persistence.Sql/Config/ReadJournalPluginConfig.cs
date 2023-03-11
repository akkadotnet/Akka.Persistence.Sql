using System;

namespace Akka.Persistence.Sql.Config
{
    public class ReadJournalPluginConfig
    {
        public ReadJournalPluginConfig(Configuration.Config config)
        {
            TagSeparator = config.GetString("tag-separator", ";");
            Dao = config.GetString("dao", "Akka.Persistence.Sql.Journal.Dao.ByteArrayJournalDao, Akka.Persistence.Sql");

            var tagReadStr = config.GetString("tag-read-mode", "TagTable").ToLowerInvariant();
            if (!Enum.TryParse<TagReadMode>(tagReadStr,true,out var tgr))
            {
                tgr = TagReadMode.TagTable;
            }

            TagReadMode = tgr;
        }

        public string Dao { get; }

        public string TagSeparator { get; }
        public TagReadMode TagReadMode { get; }
    }

    public enum TagReadMode
    {
        Csv,
        TagTable,
    }
}
