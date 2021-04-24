namespace SourceEngine.Demo.Stats.Models
{
    public class mapInfo
    {
        private string testDate;

        public string MapName { get; set; }

        public string WorkshopID { get; set; }

        public string GameMode { get; set; }

        public string TestType { get; set; }

        public string TestDate
        {
            get => testDate;
            set => testDate = value ?? "unknown";
        }

        public string DemoName { get; set; }

        public uint Crc { get; set; }
    }
}
