using System;
using System.IO;
using System.Text.RegularExpressions;

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
        private static readonly Regex SedNamePattern = new(
            @"^(?<month>\d{2})_(?<day>\d{2})_(?<year>\d{4})_(?<map>[\w.-]+?)_(?<type>casual|comp)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        private static readonly Regex MapcoreNamePattern = new(
            @"^.+?-(?<year>\d{4})(?<month>\d{2})(?<day>\d{2})-\d+-\d+-workshop_\d+_(?<map>[\w.-]+?)-MAPCORE\.org.*$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
        );

        public DemoInformation() { }

        public DemoInformation(string demoPath, GameMode gameMode, TestType testType, string testDate)
        {
            if (string.IsNullOrWhiteSpace(demoPath))
                throw new ArgumentException("Path must not be null or empty.", nameof(demoPath));

            DemoName = demoPath;
            GameMode = gameMode;
            TestType = testType;
            TestDate = testDate;

            ParseFileName();
        }

        public string DemoName { get; set; }

        public string MapName { get; set; } = "unknown";

        public GameMode GameMode { get; set; }

        public TestType TestType { get; set; }

        public string TestDate { get; set; }

        private void ParseFileName()
        {
            string fileName = Path.GetFileNameWithoutExtension(DemoName);
            if (Guid.TryParse(fileName, out Guid _))
                return; // Faceit demo. No further information can be derived from the file name.

            Match sedMatch = SedNamePattern.Match(fileName);

            if (ParseFileNameMatch(sedMatch))
            {
                if (TestType is TestType.Unknown)
                    TestType = sedMatch.Groups["type"].Value == "casual" ? TestType.Casual : TestType.Competitive;
            }
            else
            {
                ParseFileNameMatch(MapcoreNamePattern.Match(fileName));
            }
        }

        private bool ParseFileNameMatch(Match match)
        {
            if (!match.Success)
                return false;

            TestDate ??= $"{match.Groups["day"]}/{match.Groups["month"]}/{match.Groups["year"]}";
            MapName = match.Groups["map"].Value;
            return true;
        }
    }
}
