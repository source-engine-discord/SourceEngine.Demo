using SourceEngine.Demo.Parser;

namespace SourceEngine.Demo.Stats.Models
{
    public class BombPlanted
    {
        public int Round { get; set; }
        public double TimeInRound { get; set; }
        public Player Player { get; set; }
        public char? Bombsite { get; set; } // null when not planted at a bombsite, eg. danger zone
        public double XPosition { get; set; }
        public double YPosition { get; set; }
        public double ZPosition { get; set; }

        public BombPlanted() { }
    }
}
