using System.Collections.Generic;

namespace SourceEngine.Demo.Stats.Models
{
    public class DataAndPlayerNames
    {
        public Dictionary<long, Dictionary<string, long>> Data { get; set; }

        public Dictionary<long, Dictionary<string, string>> PlayerNames { get; set; }
    }
}
