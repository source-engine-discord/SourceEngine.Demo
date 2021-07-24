using System;
using System.IO;

using Newtonsoft.Json;

using SourceEngine.Demo.Stats.Models;

namespace SourceEngine.Demo.Stats.App
{
    internal class Writer
    {
        private readonly string outputRoot;
        private readonly bool sameFileName;
        private readonly bool sameFolderStructure;

        public Writer(string outputRoot, bool sameFileName, bool sameFolderStructure)
        {
            this.outputRoot = outputRoot;
            this.sameFileName = sameFileName;
            this.sameFolderStructure = sameFolderStructure;
        }

        public void Write(object stats, DemoInformation demoInfo, string mapName, string suffix = "")
        {
            string path = GetOutputPathWithoutExtension(demoInfo, mapName);
            Write(stats, $"{path}{suffix}.json");
        }

        private string GetOutputPathWithoutExtension(DemoInformation demoInfo, string mapName)
        {
            string path = outputRoot;

            // A null relative path means the full path to the demo file was provided by the user.
            if (sameFolderStructure && demoInfo.RelativePath is not null)
            {
                // It can only return null if the demo happens to be directly under outputRoot.
                // If it lacks directory info, then just let it be an empty string so it gets written under outputRoot.
                string demoRelativeDir = Path.GetDirectoryName(demoInfo.RelativePath) ?? "";

                path = Path.Join(outputRoot, demoRelativeDir);
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);
            }

            string filename = sameFileName
                ? Path.GetFileNameWithoutExtension(demoInfo.DemoName)
                : Guid.NewGuid().ToString();

            filename = mapName + "_" + filename;

            if (!string.IsNullOrWhiteSpace(demoInfo.TestDate))
                filename = demoInfo.TestDate.Replace('/', '_') + "_" + filename;

            return Path.Join(path, filename);
        }

        private static void Write(object stats, string path)
        {
            using var sw = new StreamWriter(path, false);
            string json = JsonConvert.SerializeObject(stats, Formatting.Indented);
            sw.WriteLine(json);
        }
    }
}
