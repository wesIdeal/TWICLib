using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using HtmlAgilityPack;


namespace TWICLib
{
    public enum Response
    {
        [Description("Ok")] Ok,

        [Description("No newer items found in search")]
        NoNewerItemsFound,

        [Description("No items found in list. List must be initialized.")]
        NoItemsInitialized,

        [Description("No items found in specified date range.")]
        NoDateRangeItemsFound,
    }

    public class EntryRetriever
    {
        private const string TWICArchiveUrl = "http://theweekinchess.com/twic";
        private const int IdColumnNumber = 0;
        private const int FileDateColumnNumber = 1;
        private const int PgnColumnNumber = 4;
        private const int CbvColumnNumber = 5;
        private static readonly CultureInfo DateFormatProvider = CultureInfo.InvariantCulture;

        public EntryRetriever(string twicUri = "")
        {
            Entries = new List<TWICEntry>();
            if (twicUri == "")
            {
                twicUri = TWICArchiveUrl;
            }

        }

        public List<TWICEntry> Entries { get; internal set; }

        public void Initialize()
        {
            Initialize(TWICArchiveUrl);
        }

        public List<TWICEntry> Initialize(string twicUri)
        {
            var web = new HtmlWeb();
            var doc = web.Load(twicUri);
            var table = doc.DocumentNode.SelectSingleNode("//table[contains(@class, 'results-table')]");
            var tableBody = table.SelectSingleNode("//tbody");
            var rows = tableBody.SelectNodes("//tr").ToList();
            var entries = new List<TWICEntry>();
            rows
                .Where(r => r.ChildNodes.Any(cn => cn.Name == "td"))
                .Select((node, i) => new { idx = i, rowContents = node })
                .ToList()
                .ForEach((node) =>
            {
                Debug.WriteLine($"{node.idx}:\t{node.rowContents}");
                var cells = node.rowContents.SelectNodes("td").ToArray();
                var id = GetRowId(cells);
                if (!id.HasValue) return;
                var date = DateTime.ParseExact(cells[FileDateColumnNumber].InnerText, "dd/MM/yyyy",
                    DateFormatProvider);
                var pgnUri = GetUriFromColumn(cells[PgnColumnNumber]);
                var cbvUri = GetUriFromColumn(cells[CbvColumnNumber]);
                entries.Add(new TWICEntry()
                {
                    ID = id.Value,
                    PGNUri = pgnUri,
                    CBVUri = cbvUri,
                    PublishDate = date
                });
            });
            return Entries = entries;
        }

        private static int? GetRowId(HtmlNode[] cells)
        {
            var idText = cells[IdColumnNumber].InnerText;
            if (!int.TryParse(idText, out var id))
            {
                return null;
            }
            return id;
        }

        private static Uri GetUriFromColumn(HtmlNode nodeWithUri) => new Uri(nodeWithUri.SelectSingleNode("a").Attributes["href"].Value);

        public void GetDownloadListByIdRange(int idFrom, int? idTo, out List<TWICEntry> entries)
        {
            entries = Entries.Where(x => x.ID >= idFrom).ToList();
            if (idTo.HasValue)
            {
                entries = entries.Where(x => x.ID <= idTo).ToList();
            }
        }

        public Response GetDownloadListById(int id, out List<TWICEntry> entries)
        {
            entries = null;
            if (!Entries.Any())
            {
                return Response.NoItemsInitialized;
            }

            entries = new List<TWICEntry>();
            if (Entries.All(x => x.ID != id))
            {
                Debug.WriteLine($"It appears as if no TWIC archives exist with the id of {id}.");
                return Response.NoNewerItemsFound;
            }
            else
            {
                entries = Entries.Where(x => x.ID == id).OrderBy(x => x.ID).ToList();
            }

            return Response.Ok;
        }

        public Response GetDownloadListById(int? lastDownloaded, out List<TWICEntry> entries)
        {
            entries = null;
            if (!Entries.Any())
            {
                return Response.NoItemsInitialized;
            }

            entries = new List<TWICEntry>();
            if (!Entries.Any(x => x.ID > lastDownloaded))
            {
                Debug.WriteLine($"It appears as if no TWIC archives exist which are newer than {lastDownloaded}.");
                return Response.NoNewerItemsFound;
            }
            else
            {
                entries = entries.Where(x => x.ID > lastDownloaded).OrderBy(x => x.ID).ToList();
            }

            return Response.Ok;
        }

        public Response GetDownloadListByDateRange(DateTime startDate, DateTime? endDate,
            out List<TWICEntry> entriesOut, out string message)
        {
            message = null;
            entriesOut = new List<TWICEntry>();
            if (!Entries.Any())
            {
                return Response.NoItemsInitialized;
            }


            if (!endDate.HasValue)
            {
                endDate = DateTime.MaxValue;
            }

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
                return Response.NoDateRangeItemsFound;
            }

            return Response.Ok;
        }
    }
}