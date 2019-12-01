using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using CommandLine;
using Microsoft.Extensions.Configuration;
using TWICLib;

namespace TWICHelper
{
    internal class TWICHelper
    {
        private static string _outputDir = "";
        private static CommandLineArgs _commandLineOpts = new CommandLineArgs();
        private static readonly ManualResetEvent AllDone = new ManualResetEvent(false);
        public static UserSettings SettingsConfig;
        private static bool _overwriteAll;
        private const string ConfigurationFilename = "appsettings.json";

        static TWICHelper()
        {
            SettingsConfig = new UserSettings();
            IConfiguration configuration = new ConfigurationBuilder().AddJsonFile(ConfigurationFilename, false, true).Build();
            configuration.GetSection("Settings").Bind(SettingsConfig);
        }

        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<CommandLineArgs>(args).WithParsed(
                options =>
                {
                    _commandLineOpts = options;
                    var entryRetriever = new EntryRetriever();
                    var entries = new List<TWICEntry>();
                    _outputDir = GetOutputDirectory();
                    if (string.IsNullOrWhiteSpace(_outputDir))
                    {
                        return;
                    }

                    entryRetriever.Initialize();
                    if (_commandLineOpts.Id.HasValue)
                    {
                        entryRetriever.GetDownloadListById(_commandLineOpts.Id.Value, out entries);
                    }
                    else if (_commandLineOpts.DownloadFrom.HasValue)
                    {
                        entryRetriever.GetDownloadListByIdRange(_commandLineOpts.DownloadFrom.Value,
                            _commandLineOpts.DownloadTo,
                            out entries);
                    }

                    var downloadList = entries.Select(x => x.PGNUri).ToList();
                    if (downloadList.Any())
                    {
                        Console.WriteLine($"You are about to download {downloadList.Count} archives. Continue?");
                        var continueProcessing = ConsoleHelper.GetContinue();
                        if (continueProcessing)
                        {
                            foreach (var uri in downloadList)
                            {
                                Console.WriteLine($"Downloading {uri}.");

                                GetRequestStreamCallback(uri);
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("No files to download.");
                    }
                });

            if (Debugger.IsAttached)
            {
                Console.WriteLine("Press any key.");
                Console.ReadKey();
            }
        }

        private static string GetOutputDirectory()
        {
            var outputDirectory =
             _commandLineOpts.OutputDirectory;
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                outputDirectory = Path.GetTempPath();
                Console.WriteLine($"No output specified. Using {outputDirectory}.{Environment.NewLine}Continue?{Environment.NewLine}");
                if (!ConsoleHelper.GetContinue())
                {
                    Console.WriteLine("Use command line option --output to specify the output directory.");
                    return null;
                }
            }
            if (!Directory.Exists(outputDirectory))
            {
                Console.WriteLine($"Warning: {outputDirectory} does not exist. Create this directory?");
                if (ConsoleHelper.GetContinue())
                {
                    Directory.CreateDirectory(outputDirectory);
                }
                else
                {
                    return null;
                }
            }
            return outputDirectory;
        }

        private static void GetRequestStreamCallback(Uri request)
        {
            var fileName = request.Segments[^1];
            var outputPath = Path.Combine(_outputDir, fileName);
            if (File.Exists(outputPath) && !_overwriteAll)
            {
                Console.WriteLine($"File {outputPath} exists. Overwrite? (y|a(ll)|n");
                var key = char.ToLower(Console.ReadKey().KeyChar);
                var continueOverwrite = key == 'y';
                _overwriteAll = key == 'a';
                if (!continueOverwrite && !_overwriteAll) return;
            }
            // End the operation

            long received = 0;
            var buffer = new byte[1024];

            Console.WriteLine($"\r\nFile {fileName}");
            try
            {
                var response = GetResponse(request);
                var total = response.ContentLength;

                using var fileStream = new FileStream(outputPath, FileMode.OpenOrCreate, FileAccess.Write);
                using var input = response.GetResponseStream();
                if (input == null)
                {
                    throw new ApplicationException("Error: response was null.");
                }
                var size = input.Read(buffer, 0, buffer.Length);
                while (size > 0)
                {
                    fileStream.Write(buffer, 0, size);
                    received += size;
                    var progressPercent = (float)received / total;
                    var displayPercent = (int)Math.Round(25 * progressPercent);
                    Console.Write(
                        $"\rProgress: [{new string('X', displayPercent).PadRight(25, '_')}] {Math.Round(progressPercent * 100, 2)}%   ");
                    size = input.Read(buffer, 0, buffer.Length);
                }

                Console.WriteLine("Finished writing " + outputPath + Environment.NewLine);

                fileStream.Flush();
                fileStream.Close();
                if (_commandLineOpts.Unzip)
                {
                    ZipFile.ExtractToDirectory(outputPath, _outputDir, true);
                    if (_commandLineOpts.Cleanup)
                    {
                        File.Delete(outputPath);
                    }
                }

                // Release the HttpWebResponse
                response.Close();

                AllDone.Set();
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
                throw;
            }
        }

        private static WebResponse GetResponse(Uri requestUri)
        {
            while (true)
            {
                try
                {
                    if (WebRequest.Create(requestUri) is HttpWebRequest request) return request.GetResponse();
                }
                catch (WebException exc)
                {
                    if (exc.Response != null && exc.Response.GetType() == typeof(HttpWebResponse))
                    {
                        if (exc.Response is HttpWebResponse resp && (resp.StatusCode == HttpStatusCode.Moved ||
                                             resp.StatusCode == HttpStatusCode.MovedPermanently))
                        {
                            requestUri = new Uri(exc.Response.Headers["Location"]);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

            }
        }


        private static Tuple<DateTime, DateTime?> GetDateRangeFromUser(List<TWICEntry> entries)
        {
            DateTime startDate;
            DateTime? endDate = null;
            var startDateMessage = "\r\nEnter a start date to start download (mm/dd/yyyy):\t";
            var endDateMessage =
                "Enter an end date to end download, press <enter> to get all from the provided start date. (mm/dd/yyyy):\t";
            var dateErrorMessage = "\r\nCould not parse input as a date. Try again.";
            Console.Write(startDateMessage);

            while (!DateTime.TryParse(Console.ReadLine(), out startDate))
            {
                Console.WriteLine(dateErrorMessage);
                Console.Write(startDateMessage);
            }

            Console.Write(endDateMessage);
            var endDateInput = Console.ReadLine();


            bool success;
            do
            {
                success = string.IsNullOrWhiteSpace(endDateInput);
                if (!success)
                {
                    success = DateTime.TryParse(endDateInput, out var edTemp);
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

            return new Tuple<DateTime, DateTime?>(startDate, endDate);

        }
    }
}