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
namespace TWICHelper
{
    class TWICHelper
    {
        private static IConfiguration Configuration;
        private static List<TWICEntry> Entries;
        private static readonly CultureInfo DateFormatProvider = CultureInfo.InvariantCulture;

        public static UserSettings settingsConfig;
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
            var entries = entryRetriever.Entries;
            var iniConfigParser = new FileIniDataParser();
            var parserData = iniConfigParser.ReadFile("config.ini");
            Console.WriteLine($"You are about to download {downloadList.Count()} archives. Continue?");
            var continueProcessing = ConsoleHelper.GetContinue();
            if (continueProcessing)
            {
                if (string.IsNullOrWhiteSpace(settingsConfig.TempDir))
                {
                    var dir = "";
                    do
                    {
                        Console.WriteLine("Please enter the directory to store the files.\t");
                        dir = Console.ReadLine();
                        if (!Directory.Exists(dir))
                        {
                            Console.WriteLine($"Cannot find director {dir}. Create it?");
                            var createDir = ConsoleHelper.GetContinue();
                            if (createDir)
                            {
                                Directory.CreateDirectory(dir);
                            }
                        }
                    } while (!Directory.Exists(dir));
                    settingsConfig.TempDir = dir;



                }
            }
            Console.ReadKey();
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

    public class UserSettings
    {
        public int? LastIdDownloaded { get; set; }
        public string TempDir { get; set; }
    }
}
