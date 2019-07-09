using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class RoundsStats
    {
        public string Round { get; set; }
        public string Half { get; set; }
        public string Winners { get; set; }
        public string WinMethod { get; set; }
        public int TeamAlphaEquipValue { get; set; }
        public int TeamBetaEquipValue { get; set; }
        public int TeamAlphaExpenditure { get; set; }
        public int TeamBetaExpenditure { get; set; }

        public RoundsStats() { }
    }
}
