using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SourceEngine.Demo.Stats.Models
{
    public class roundsStats
    {
        public int Round { get; set; }
        public string Half { get; set; }
        public int Overtime { get; set; }
        public double Length { get; set; }
        public string Winners { get; set; }
        public string WinMethod { get; set; }
        public string BombsitePlantedAt { get; set; }
		public double? BombPlantPositionX { get; set; }
		public double? BombPlantPositionY { get; set; }
		public double? BombPlantPositionZ { get; set; }
		public string BombsiteErrorMessage { get; set; }
        public bool PickedUpHostageA { get; set; }
        public bool PickedUpHostageB { get; set; }
        public bool PickedUpAllHostages { get; set; }
        public string HostageAPickedUpErrorMessage { get; set; }
        public string HostageBPickedUpErrorMessage { get; set; }
		public bool RescuedHostageA { get; set; }
        public bool RescuedHostageB { get; set; }
        public bool RescuedAllHostages { get; set; }
        public double? RescuedHostageAPositionX { get; set; }
        public double? RescuedHostageAPositionY { get; set; }
        public double? RescuedHostageAPositionZ { get; set; }
        public double? RescuedHostageBPositionX { get; set; }
        public double? RescuedHostageBPositionY { get; set; }
        public double? RescuedHostageBPositionZ { get; set; }
        public double? TimeInRoundPlanted { get; set; }
        public double? TimeInRoundExploded { get; set; }
        public double? TimeInRoundDefused { get; set; }
        public double? TimeInRoundRescuedHostageA { get; set; }
        public double? TimeInRoundRescuedHostageB { get; set; }
        public int TeamAlphaPlayerCount { get; set; }
        public int TeamBetaPlayerCount { get; set; }
        public int TeamAlphaEquipValue { get; set; }
        public int TeamBetaEquipValue { get; set; }
        public int TeamAlphaExpenditure { get; set; }
        public int TeamBetaExpenditure { get; set; }

        public roundsStats() { }
    }
}
