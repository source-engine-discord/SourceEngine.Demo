using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using DemoInfo;
using System.Threading;
using Newtonsoft.Json;

namespace TopStatsWaffle
{
    public class Config
    {
        public string apikey;

        public Config(string path)
        {
            StreamReader sr = new StreamReader(path);
            this.apikey = sr.ReadLine();
            sr.Close();

            if (this.apikey == null || this.apikey == "")
            {
                Debug.Error("STEAM KEY NOT SUPPLIED. Press enter to exit...");
                Console.ReadLine();
                Environment.Exit(1);
            }
        }
    }

    class Program
    {

        static void Main(string[] args)
        {
            ensureConfigExists();
            Config cfg = new Config("config.cfg");
            Collector c = new Collector("demos", cfg.apikey);

            c.attachAll();

            c.EventSubscription += (EventSubscriptionEventArgs ev) =>
            {

            };

            c.Process();
        }

        static void ensureConfigExists()
        {
            if (!File.Exists("config.cfg"))
            {
                Debug.Error("config.cfg cannot be found!!! Creating one...");

                StreamWriter sw = new StreamWriter("config.cfg");

                Debug.Blue("Enter Steam API key: ");
                string apiKey = Console.ReadLine();

                sw.WriteLine(apiKey);

                sw.Close();

                Debug.Success("Finished writing config...");
            }
        }
    }
}
