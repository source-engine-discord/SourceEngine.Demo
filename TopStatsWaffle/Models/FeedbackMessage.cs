using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class FeedbackMessage
    {
        public string Round { get; set; }
        public long SteamID { get; set; }
        public string TeamName { get; set; }
        public string Message { get; set; }

        public FeedbackMessage() { }
    }
}
