namespace SourceEngine.Demo.Stats.Models
{
    public enum GameMode
    {
        Unknown = -1,
        DangerZone,
        Defuse,
        Hostage,
        WingmanDefuse,
        WingmanHostage,
    }

    public enum TestType
    {
        Unknown = -1,
        Casual,
        Competitive,
    }

    public class DemoInformation
    {
        public string DemoName { get; set; }

        public string MapName { get; set; }

        public GameMode GameMode { get; set; }

        public TestType TestType { get; set; }

        public string TestDate { get; set; }
    }
}
