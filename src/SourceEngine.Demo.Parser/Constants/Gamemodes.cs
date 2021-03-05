using System.Collections.Generic;

namespace SourceEngine.Demo.Parser.Constants
{
    public static class Gamemodes
    {
        public const string Defuse = "defuse";
        public const string Hostage = "hostage";
        public const string WingmanDefuse = "wingmandefuse";
        public const string WingmanHostage = "wingmanhostage";
        public const string DangerZone = "dangerzone";

        public const string Unknown = "unknown";

        public static List<string> GetAll()
        {
            return new()
            {
                Defuse,
                Hostage,
                WingmanDefuse,
                WingmanHostage,
                DangerZone,
            };
        }

        public static List<string> HaveBombsites()
        {
            return new()
            {
                Defuse,
                WingmanDefuse,
                Unknown,
            };
        }

        public static List<string> HaveHostages()
        {
            return new()
            {
                Hostage,
                WingmanHostage,
                DangerZone,
                Unknown,
            };
        }
    }
}
