using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class TeamEquipmentStats
    {
        public int Round { get; set; }
        public int TEquipValue { get; set; }
        public int CTEquipValue { get; set; }
        public int TExpenditure { get; set; }
        public int CTExpenditure { get; set; }

        public TeamEquipmentStats() { }
    }
}
