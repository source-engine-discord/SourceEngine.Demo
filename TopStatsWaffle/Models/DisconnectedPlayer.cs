using DemoInfo;

namespace TopStatsWaffle.Models
{
    public class DisconnectedPlayer
    {
        public PlayerDisconnectEventArgs PlayerDisconnectEventArgs { get; set; }
        public int Round { get; set; }

        public DisconnectedPlayer() { }
    }
}
