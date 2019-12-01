using System.IO;
using CommandLine;

namespace TWICHelper
{
    public class CommandLineArgs
    {
        public static readonly string DefaultOutput = Path.GetTempPath();

        [Option("id", HelpText = "Download only this ID.")]
        public int? Id { get; set; }

        [Option("from", HelpText =
            "Download from (and including) this archive id. If no --to value is given, it will get all archives from the value specified.")]
        public int? DownloadFrom { get; set; }

        [Option("to", HelpText = "Download up to this archive id. Must be used with --from.")]
        public int? DownloadTo { get; set; }

        [Option("output", HelpText = "Directory/Folder in which downloads should be stored. If not supplied, this defaults to the user's default temp directory.'")]
        public string OutputDirectory { get; set; }

        [Option('u', "unzip", HelpText = "Unzip and delete downloads.")]
        public bool Unzip { get; set; }

        [Option("cleanup", HelpText = "Cleanup zip files. Used with --unzip option.")]
        public bool Cleanup { get; set; }
    }
}