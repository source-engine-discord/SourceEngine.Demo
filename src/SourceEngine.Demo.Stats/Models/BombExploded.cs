using SourceEngine.Demo.Parser;

namespace SourceEngine.Demo.Stats.Models
{
    public class BombExploded
    {
        public int Round { get; set; }

        public double TimeInRound { get; set; }

        public Player Player { get; set; }

        public char? Bombsite { get; set; }
    }
}
