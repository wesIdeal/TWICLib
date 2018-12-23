using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TWICLib;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Configuration;

namespace TWICLib
{
    public enum Response
    {
        [Description("Ok")]
        OK,
        [Description("No newer items found in search")]
        NO_NEWER_ITEMS_FOUND,
        [Description("No items found in list. List must be initialized.")]
        NO_ITEMS_INITIALIZED,
        [Description("No items found in specified date range.")]
        NO_DATE_RANGE_ITEMS_FOUND,
    }

    public class EntryRetriever
    {
        private static readonly CultureInfo DateFormatProvider = CultureInfo.InvariantCulture;
        public List<TWICEntry> Initialize()
        {
            var web = new HtmlAgilityPack.HtmlWeb();
            var doc = web.Load(ConfigurationManager.AppSettings["TWICArchiveURL"]);
            var table = doc.DocumentNode.SelectSingleNode($"//table[contains(@class, '{ConfigurationManager.AppSettings["TWICArchiveTableClass"]}')]");
            var rows = table.SelectNodes("//tr").ToList();
            var entries = new List<TWICEntry>();
            rows.Skip(2).Select((node, i) => new { idx = i, node }).ToList().ForEach((node) =>
            {

                var cells = node.node.SelectNodes("td").ToArray();
                var id = int.Parse(cells[0].InnerText);
                var date = DateTime.ParseExact(cells[1].InnerText, "dd/MM/yyyy", DateFormatProvider);
                var pgnUri = new Uri(cells[5].SelectSingleNode("a").Attributes["href"].Value);
                var cbvUri = new Uri(cells[6].SelectSingleNode("a").Attributes["href"].Value);
                entries.Add(new TWICEntry()
                {
                    ID = id,
                    PGNUri = pgnUri,
                    CBVUri = cbvUri,
                    PublishDate = date
                });
            });
            return entries;
        }
        public EntryRetriever()
        {
            Entries = new List<TWICEntry>();
            Initialize();
        }

        public List<TWICEntry> Entries { get; internal set; }

        public Response GetDownloadListById(int? lastDownloaded, out List<TWICEntry> entries)
        {
            entries = null;
            if (!Entries.Any()) { return Response.NO_ITEMS_INITIALIZED; }

            entries = new List<TWICEntry>();
            if (!Entries.Any(x => x.ID > lastDownloaded))
            {

                Debug.WriteLine($"It appears as if no TWIC archives exist which are newer than {lastDownloaded}.");
                return Response.NO_NEWER_ITEMS_FOUND;
            }
            else
            {
                entries = entries.Where(x => x.ID > lastDownloaded).OrderBy(x => x.ID).ToList();
            }
            return Response.OK;
        }

        public Response GetDownloadListByDateRange(DateTime startDate, DateTime? endDate, out List<TWICEntry> entriesOut, out string message)
        {

            message = null;
            entriesOut = new List<TWICEntry>();
            if (!Entries.Any()) { return Response.NO_ITEMS_INITIALIZED; }


            if (!endDate.HasValue) { endDate = DateTime.MaxValue; }
            entriesOut = Entries.Where(x => x.PublishDate > startDate && x.PublishDate <= endDate).ToList();

            if (!entriesOut.Any())
            {
                message = "It appears as if no TWIC archives exist which are ";
                if (endDate == DateTime.MaxValue)
                {
                    message += "newer than " + startDate.ToShortDateString();
                }
                else
                {
                    message += $"between {startDate.ToShortDateString()} and {endDate.Value.ToShortDateString()}";
                }
                Debug.WriteLine(message);
                return Response.NO_DATE_RANGE_ITEMS_FOUND;
            }
            return Response.OK;
        }

    }
}
