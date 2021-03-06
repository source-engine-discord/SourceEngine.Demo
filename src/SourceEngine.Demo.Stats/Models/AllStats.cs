using System.Collections.Generic;

namespace SourceEngine.Demo.Stats.Models
{
    public class AllStats
    {
        public versionNumber versionNumber { get; set; }

        public List<string> supportedGamemodes { get; set; }

        public mapInfo mapInfo { get; set; }

        public tanookiStats tanookiStats { get; set; }

        public List<playerStats> playerStats { get; set; }

        public winnersStats winnersStats { get; set; }

        public List<roundsStats> roundsStats { get; set; }

        public List<bombsiteStats> bombsiteStats { get; set; }

        public List<hostageStats> hostageStats { get; set; }

        public List<rescueZoneStats> rescueZoneStats { get; set; }

        public List<grenadesTotalStats> grenadesTotalStats { get; set; }

        public List<grenadesSpecificStats> grenadesSpecificStats { get; set; }

        public List<killsStats> killsStats { get; set; }

        public List<FeedbackMessage> feedbackMessages { get; set; }

        public chickenStats chickenStats { get; set; }

        public List<teamStats> teamStats { get; set; }

        public List<firstDamageStats> firstDamageStats { get; set; }
    }
}
