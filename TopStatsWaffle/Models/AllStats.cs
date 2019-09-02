using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class AllStats
    {
        public VersionNumber VersionNumber { get; set; }
        public List<string> SupportedGamemodes { get; set; }
        public MapInfo MapInfo { get; set; }
        public TanookiStats TanookiStats { get; set; }
        public List<PlayerStats> PlayerStats { get; set; }
        public WinnersStats WinnersStats { get; set; }
        public List<RoundsStats> RoundsStats { get; set; }
        public List<BombsiteStats> BombsiteStats { get; set; }
        public List<GrenadesTotalStats> GrenadesTotalStats { get; set; }
        public List<GrenadesSpecificStats> GrenadesSpecificStats { get; set; }
        public List<KillsStats> KillsStats { get; set; }
        public List<FeedbackMessage> FeedbackMessages { get; set; }
        public ChickenStats ChickenStats { get; set; }
        public List<TeamStats> TeamStats { get; set; }

        public AllStats() { }
    }
}
