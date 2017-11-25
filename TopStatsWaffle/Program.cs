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
    //Config class, currently only holds api key

    //Todo: add some other settings that can be written in a text file
    //Todo: add a formal config parser
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
        //Method to make sure we have a config or we will create one
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

        //Program entry point
        static void Main(string[] args)
        {

            MatchData mdTest = MatchData.fromDemoFile("demos/3_2_2017_de_rooftops_v7_comp.dem");
            mdTest.Save("Test.dat");

            return;


            // SETTINGS AND LOADING CONFIGS ----------------------------
            ensureConfigExists();
            Config cfg = new Config("config.cfg");
            Collector c = new Collector("demos");

            Steam.setAPIKey(cfg.apikey);
            if (!Steam.isSteamAPIworking())
            {
                Console.ReadLine();
                Environment.Exit(1);
            }

            //EVENT SUBSCRIPTION ---------------------------------------
            c.attachAll();

            c.EventSubscription += (EventSubscriptionEventArgs ev) =>
            {
                /*Custom event handlers here
                   ...
                */

                /* Example:
                 * 
                 *  ev.parser.PlayerDisconnect += (object sender, PlayerDisconnectEventArgs e) => { c.pushData(e.Player, "Disconnects", 1); };
                 *           ^ Runs when a player                                                   ^ Pushes data into the log          ^ Increment by this much
                 *           Disconnects                                                                                 ^ The key                                                                                                             
                */
            };

            c.Process();
        }
    }
}