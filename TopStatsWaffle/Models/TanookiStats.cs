﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TopStatsWaffle.Models
{
    public class TanookiStats
    {
        public bool Joined { get; set; }
        public bool Left { get; set; }
        public int RoundJoined { get; set; }
        public int RoundLeft { get; set; }
        public int RoundsLasted { get; set; }

        public TanookiStats() { }
    }
}