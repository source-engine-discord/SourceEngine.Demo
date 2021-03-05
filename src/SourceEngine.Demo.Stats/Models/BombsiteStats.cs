namespace SourceEngine.Demo.Stats.Models
{
    public class bombsiteStats
    {
        public char Bombsite { get; set; }

        public int Plants { get; set; }

        public int Explosions { get; set; }

        public int Defuses { get; set; }

        public double? XPositionMin { get; set; }

        public double? YPositionMin { get; set; }

        public double? ZPositionMin { get; set; }

        public double? XPositionMax { get; set; }

        public double? YPositionMax { get; set; }

        public double? ZPositionMax { get; set; }

        public bombsiteStats() { }
    }
}
