using SourceEngine.Demo.Parser;

namespace SourceEngine.Demo.Stats.Models
{
    public class DisconnectedPlayer
    {
        public DisconnectedPlayer() { }

        public PlayerDisconnectEventArgs PlayerDisconnectEventArgs { get; set; }

        public int Round { get; set; }
    }
}
