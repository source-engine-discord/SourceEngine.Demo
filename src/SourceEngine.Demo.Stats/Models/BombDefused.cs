using SourceEngine.Demo.Parser.Entities;

namespace SourceEngine.Demo.Stats.Models
{
    public class BombDefused
    {
        public int Round { get; set; }

        public double TimeInRound { get; set; }

        public Player Player { get; set; }

        public char? Bombsite { get; set; }

        public bool HasKit { get; set; }
    }
}
