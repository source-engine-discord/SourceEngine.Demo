﻿using SourceEngine.Demo.Parser.Entities;

namespace SourceEngine.Demo.Stats.Models
{
    public class HostagePickedUp
    {
        public int Round { get; set; }

        public double TimeInRound { get; set; }

        public Player Player { get; set; }

        public char Hostage { get; set; }

        public int HostageIndex { get; set; }
    }
}
