using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using HtmlAgilityPack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.FileExtensions;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using TWICLib;
using IniParser;
using IniParser.Model;
using CommandLine;
using System.Net;
using System.Threading;
using System.IO.Compression;
using System.Threading.Tasks;

namespace TWICHelper
{
    class TWICHelper
    {
        private static string outputDir = "";
        private static IConfiguration Configuration;
        private static List<TWICEntry> Entries;
        static CommandLineArgs commandLineOpts = new CommandLineArgs();
        private static readonly CultureInfo DateFormatProvider = CultureInfo.InvariantCulture;
        private static ManualResetEvent allDone = new ManualResetEvent(false);
        public static UserSettings settingsConfig;
        static bool overwriteAll = false;
        static TWICHelper()
        {

            settingsConfig = new UserSettings();
            Configuration = new ConfigurationBuilder().AddJsonFile("appsettings.json", false, true).Build();
            Configuration.GetSection("Settings").Bind(settingsConfig);


        }
        static void Main(string[] args)
        {

            var downloadList = new List<Uri>();
            var entryRetriever = new EntryRetriever();
            CommandLine.Parser.Default.ParseArguments<CommandLineArgs>(args).WithParsed(options => { commandLineOpts = options; });
            var entries = new List<TWICEntry>();
            outputDir = commandLineOpts.OutputPath;
            if (commandLineOpts.Id.HasValue)
            {
                entryRetriever.GetDownloadListById(commandLineOpts.Id.Value, out entries);

            }
            else if (commandLineOpts.DownloadFrom.HasValue)
            {
                entryRetriever.GetDownloadListByIdRange(commandLineOpts.DownloadFrom.Value, commandLineOpts.DownloadTo, out entries);
            }
            downloadList = entries.Select(x => x.PGNUri).ToList();
            if (downloadList.Any())
            {
                Console.WriteLine($"You are about to download {downloadList.Count()} archives. Continue?");
                var continueProcessing = ConsoleHelper.GetContinue();
                if (continueProcessing)
                {

                    foreach (var uri in downloadList)
                    {
                        Console.WriteLine($"Downloading {uri}.");
                        var request = WebRequest.Create(uri) as HttpWebRequest;
                        GetRequestStreamCallback(request);


                    }
                }
            }
            else { Console.WriteLine("No files to download."); }
            if (Debugger.IsAttached)
            {
                Console.WriteLine("Press any key.");
                Console.ReadKey();
            }
        }

        private static void GetRequestStreamCallback(HttpWebRequest request)
        {
            var fileName = request.RequestUri.Segments[(request.RequestUri.Segments.Length - 1)];
            var outputPath = Path.Combine(outputDir, fileName);
            if (File.Exists(outputPath) && !overwriteAll)
            {
                Console.WriteLine($"File {outputPath} exists. Overwrite? (y|a(ll)|n");
                var key = Char.ToLower(Console.ReadKey().KeyChar);
                var continueOverwrite = key == 'y';
                overwriteAll = key == 'a';
                if (!continueOverwrite && !overwriteAll) return;
            }
            // End the operation

            long total = 0;
            long received = 0;
            byte[] buffer = new byte[1024];

            Console.WriteLine($"\r\nFile {fileName}");
            try
            {
                var response = request.GetResponse();
                using (FileStream fileStream = new FileStream(outputPath, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    using (Stream input = response.GetResponseStream())
                    {
                        total = response.ContentLength;

                        int size = input.Read(buffer, 0, buffer.Length);
                        while (size > 0)
                        {
                            fileStream.Write(buffer, 0, size);
                            received += size;
                            var progressPercent = (float)received / total;
                            var displayPercent = (int)Math.Round(25 * progressPercent);
                            Console.Write($"\rProgress: [{new string('X', displayPercent).PadRight(25, '_') }] {Math.Round(progressPercent * 100, 2)}%   ");
                            size = input.Read(buffer, 0, buffer.Length);
                        }

                    }
                    Console.WriteLine("Finished writing " + outputPath + Environment.NewLine);

                    fileStream.Flush();
                    fileStream.Close();
                    if (commandLineOpts.Unzip)
                    {
                        ZipFile.ExtractToDirectory(outputPath, outputDir, true);
                        if (commandLineOpts.Cleanup)
                        {
                            File.Delete(outputPath);
                        }
                    }
                    // Release the HttpWebResponse
                    response.Close();

                    allDone.Set();


                }
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                throw;
            }
        }


        private static Tuple<DateTime, DateTime?> GetDateRangeFromUser(List<TWICEntry> entries)
        {

            DateTime startDate;
            DateTime? endDate = null;
            List<Uri> downloadList = new List<Uri>();
            var startDateMessage = "\r\nEnter a start date to start download (mm/dd/yyyy):\t";
            var endDateMessage = "Enter an end date to end download, press <enter> to get all from the provided start date. (mm/dd/yyyy):\t";
            var dateErrorMessage = "\r\nCould not parse input as a date. Try again.";
            Console.Write(startDateMessage);

            while (!DateTime.TryParse(Console.ReadLine(), out startDate))
            {
                Console.WriteLine(dateErrorMessage);
                Console.Write(startDateMessage);
            }
            var tmpDlList = entries.Where(e => e.PublishDate >= startDate);
            Console.Write(endDateMessage);
            var endDateInput = Console.ReadLine();


            var success = false;
            do
            {
                DateTime edTemp;
                success = string.IsNullOrWhiteSpace(endDateInput);
                if (!success)
                {
                    DateTime.TryParse(endDateInput, out edTemp);
                    if (!success)
                    {
                        Console.WriteLine(dateErrorMessage);
                        Console.Write(endDateMessage);
                        endDateInput = Console.ReadLine();
                    }
                    else
                    {
                        endDate = edTemp;
                    }
                }
            } while (!success);

            return new Tuple<DateTime, DateTime?>(startDate, endDate); ;
        }


    }

    public class CommandLineArgs
    {
        [Option("id", HelpText = "Download only this ID.")]
        public int? Id { get; set; }

        [Option("from", HelpText = "Download from (and including) this archive. If no --to value is given, it will get all archives from the value specified.")]
        public int? DownloadFrom { get; set; }
        [Option("to", HelpText = "Download up to this number. Must be used with --from.")]
        public int? DownloadTo { get; set; }
        [Option("output", HelpText = "Path in which downloads are stored. Path must exist.")]
        public string OutputPath { get; set; }
        [Option('u', longName: "unzip", HelpText = "Unzip and delete downloads.")]
        public bool Unzip { get; set; }

        [Option("cleanup", HelpText = "Cleanup zip files. Used with --unzip option.")]
        public bool Cleanup { get; set; }
    }

    public class UserSettings
    {

        public int? LastIdDownloaded { get; set; }
        public string TempDir { get; set; }
    }
}
