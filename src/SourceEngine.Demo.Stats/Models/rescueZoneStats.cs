namespace SourceEngine.Demo.Stats.Models
{
	public class rescueZoneStats
    {
        public int? rescueZoneIndex { get; set; } // doesn't line up with the trigger Entity ID
        public double? XPositionMin { get; set; }
        public double? YPositionMin { get; set; }
        public double? ZPositionMin { get; set; }
        public double? XPositionMax { get; set; }
        public double? YPositionMax { get; set; }
        public double? ZPositionMax { get; set; }
    }
}
