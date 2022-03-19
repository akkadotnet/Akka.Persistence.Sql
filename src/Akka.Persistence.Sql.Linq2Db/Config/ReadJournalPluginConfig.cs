using System;

namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class ReadJournalPluginConfig
    {
        public ReadJournalPluginConfig(Configuration.Config config)
        {
            TagSeparator = config.GetString("tag-separator", ",");
            Dao = config.GetString("dao",
                "akka.persistence.sql.linq2db.dao.bytea.readjournal.bytearrayreadjournaldao");
            var tagReadStr = config.GetString("tag-read-mode", "default");
            if (Enum.TryParse<TagReadMode>(tagReadStr,true,out TagReadMode tgr))
            {
                
            }
            else if (tagReadStr.Equals("default", StringComparison.InvariantCultureIgnoreCase))
            {
                tgr = TagReadMode.CommaSeparatedArray;
            }
            else if (tagReadStr.Equals("migrate", StringComparison.InvariantCultureIgnoreCase))
            {
                tgr = TagReadMode.CommaSeparatedArrayAndTagTable;
            }

            TagReadMode = tgr;
        }

        public string Dao { get; set; }

        public string TagSeparator { get; set; }
        public TagReadMode TagReadMode { get; set; }
    }

    [Flags]
    public enum TagReadMode
    {
        CommaSeparatedArray = 1,
        TagTable = 2,
        CommaSeparatedArrayAndTagTable = 3
    }
}