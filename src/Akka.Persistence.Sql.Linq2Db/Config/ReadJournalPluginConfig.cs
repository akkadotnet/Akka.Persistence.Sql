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
            var tagReadStr = config.GetString("tag-read-mode", "defaultconcatvarchar");
            if (Enum.TryParse<TagReadMode>(tagReadStr,true,out TagReadMode tgr))
            {
                
            }
            else if (tagReadStr.Equals("default", StringComparison.InvariantCultureIgnoreCase))
            {
                tgr = TagReadMode.DefaultConcatVarchar;
            }
            else if (tagReadStr.Equals("migrate", StringComparison.InvariantCultureIgnoreCase))
            {
                tgr = TagReadMode.MigrateToTagTable;
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
        DefaultConcatVarchar = 1,
        MigrateToTagTable = 3,
        TagTableOnly = 2
    }
}