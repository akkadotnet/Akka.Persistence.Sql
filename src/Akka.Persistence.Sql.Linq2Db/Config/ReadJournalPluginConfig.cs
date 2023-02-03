using System;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class ReadJournalPluginConfig
    {
        public ReadJournalPluginConfig(Configuration.Config config)
        {
            TagSeparator = config.GetString("tag-separator", ";");
            Dao = config.GetString("dao", "Akka.Persistence.Sql.Linq2Db.Journal.Dao.ByteArrayJournalDao, Akka.Persistence.Sql.Linq2Db");
            
            var tagReadStr = config.GetString("tag-read-mode", "csv").ToLowerInvariant();
            if (!Enum.TryParse<TagReadMode>(tagReadStr,true,out var tgr))
            {
                tgr = TagReadMode.Csv;
            }

            TagReadMode = tgr;
        }

        public string Dao { get; }

        public string TagSeparator { get; }
        public TagReadMode TagReadMode { get; }
    }
    
    public enum TagReadMode
    {
        Csv = 1,
        TagTable = 2,
    }
}