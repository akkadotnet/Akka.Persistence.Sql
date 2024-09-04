// -----------------------------------------------------------------------
//  <copyright file="Options.cs" company="Akka.NET Project">
//      Copyright (C) 2013-2023 .NET Foundation <https://github.com/akkadotnet/akka.net>
//  </copyright>
// -----------------------------------------------------------------------

using CommandLine;

namespace Akka.Persistence.Sql.IndexHelperApp
{
    public class Options
    {
        [Option(
            'f',
            "file",
            Required = true,
            HelpText = "Specify the HOCON file to use")]
        public string File { get; set; } = string.Empty;

        [Option(
            'p',
            "path",
            Required = true,
            HelpText = "The Path to the Akka.Persistence.Sql Config in the HOCON.")]
        public string HoconPath { get; set; } = string.Empty;

        [Option(
            "OrderingIdx",
            Required = true,
            Group = "IndexType",
            HelpText = "Generates the SQL Text for an Ordering index")]
        public bool GenerateOrdering { get; set; }

        [Option(
            "PidSeqNoIdx",
            Required = true,
            Group = "IndexType",
            HelpText = "Generates the SQL Text for an index on PersistenceID and SeqNo")]
        public bool GeneratePidSeqNo { get; set; }

        [Option(
            "TimeStampIdx",
            Required = true,
            Group = "IndexType",
            HelpText = "Generates the SQL Text for a Timestamp Index")]
        public bool GenerateTimestamp { get; set; }
    }
}
