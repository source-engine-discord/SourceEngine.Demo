using DemoInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class DisconnectedPlayer
    {
        public PlayerDisconnectEventArgs PlayerDisconnectEventArgs { get; set; }
        public int Round { get; set; }

        public DisconnectedPlayer() { }
    }
}
