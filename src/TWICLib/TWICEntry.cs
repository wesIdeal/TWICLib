using System;

namespace TWICLib
{
    public class TWICEntry
    {
        public int ID { get; set; }
        public DateTime PublishDate { get; set; }
        public Uri PGNUri { get; set; }
        public Uri CBVUri { get; set; }

        public override string ToString()
        {
            return $"Id: {ID} | Publish Date: {PublishDate.ToShortDateString()} | PGN: {PGNUri} | CBV: {CBVUri}";
        }
    }
}
