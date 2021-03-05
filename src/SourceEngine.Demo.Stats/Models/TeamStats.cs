using System.Collections.Generic;

namespace SourceEngine.Demo.Stats.Models
{
    public class teamStats
    {
        public teamStats() { }

        public int Round { get; set; }

        public IEnumerable<long> TeamAlpha { get; set; }

        public int TeamAlphaKills { get; set; }

        public int TeamAlphaDeaths { get; set; }

        public int TeamAlphaAssists { get; set; }

        public int TeamAlphaFlashAssists { get; set; }

        public int TeamAlphaHeadshots { get; set; }

        public int TeamAlphaTeamkills { get; set; }

        public int TeamAlphaSuicides { get; set; }

        public int TeamAlphaWallbangKills { get; set; }

        public int TeamAlphaWallbangsTotalForAllKills { get; set; }

        public int TeamAlphaWallbangsMostInOneKill { get; set; }

        public int TeamAlphaShotsFired { get; set; }

        public IEnumerable<long> TeamBravo { get; set; }

        public int TeamBravoKills { get; set; }

        public int TeamBravoDeaths { get; set; }

        public int TeamBravoAssists { get; set; }

        public int TeamBravoFlashAssists { get; set; }

        public int TeamBravoHeadshots { get; set; }

        public int TeamBravoTeamkills { get; set; }

        public int TeamBravoSuicides { get; set; }

        public int TeamBravoWallbangKills { get; set; }

        public int TeamBravoWallbangsTotalForAllKills { get; set; }

        public int TeamBravoWallbangsMostInOneKill { get; set; }

        public int TeamBravoShotsFired { get; set; }
    }
}
