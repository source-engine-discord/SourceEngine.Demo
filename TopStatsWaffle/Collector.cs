using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DemoInfo;
using System.IO;
using Newtonsoft.Json;

namespace TopStatsWaffle
{
    public class PlayerData
    {
        public long s_steamid;
        public string s_steamname;

        public Dictionary<int, Dictionary<string, int>> collected = new Dictionary<int, Dictionary<string, int>>();

        public PlayerData(long steamid, string name = "")
        {
            this.s_steamid = steamid;
            this.s_steamname = name;
        }

        public Dictionary<string, int> getTotalData()
        {
            Dictionary<string, int> total = new Dictionary<string, int>();

            //Iterate each matchkey
            foreach (int matchKey in collected.Keys)
            {
                //Iterate each key in the collected keyss 
                foreach (string attribKey in collected[matchKey].Keys)
                {
                    //If the key doesn't exist create it
                    if (!total.ContainsKey(attribKey))
                        total.Add(attribKey, 0);

                    //Then concat our value
                    total[attribKey] += collected[matchKey][attribKey];
                }
            }

            //Return the data
            return total;
        }
    }

    class RecorderSettings
    {
        public int matchID = 0;
        public Dictionary<int, long> playerLookups = new Dictionary<int, long>();
    }

    static class EXTMethods
    {
        public static string getAttribCSVrow(this Dictionary<string, int> block, List<string> headerKeys, long steamid, string name = "")
        {

            string built = steamid.ToString() + ",";

            if (name != "")
                built += name + ",";

            //Iterate each header and check if its present in the data block
            //Then add it to the CSV string
            foreach (string header in headerKeys)
            {
                if (block.ContainsKey(header))
                    built += block[header] + ",";
                else
                    built += "0,";
            }

            //Cut off the last ,
            return built.Substring(0, built.Length - 1);
        }

        public static PlayerData fromSteamID(this List<PlayerData> players, long steamid)
        {
            foreach (PlayerData dat in players)
                if (dat.s_steamid == steamid)
                    return dat;

            return null;
        }

        [Obsolete("Move to new method")]
        public static void appendValue(this List<PlayerData> players, Player player, RecorderSettings rs, string attrib, int value)
        {
            if (player == null)
                return;

            //Steam ID checks
            //76561198056991900
            long steamid = player.SteamID;

            if (steamid == 0)
                if (rs.playerLookups.ContainsKey(player.EntityID))
                    steamid = rs.playerLookups[player.EntityID];
                else
                    return;

            //Add the player if they don't exist in memory
            if (players.fromSteamID(steamid) == null)
                players.Add(new PlayerData(steamid));

            //Create reference to the player
            PlayerData dat = players.fromSteamID(steamid);

            //If the match doesn't exist on the player then we add it
            if (!dat.collected.ContainsKey(rs.matchID))
                dat.collected.Add(rs.matchID, new Dictionary<string, int>());

            //If the attribute doesn't exist on the playerdata's match then we add it
            if (!dat.collected[rs.matchID].ContainsKey(attrib))
                dat.collected[rs.matchID].Add(attrib, 0);

            //Add the value onto the total
            dat.collected[rs.matchID][attrib] += value;
        }

        public static List<string> getAllHeaders(this List<PlayerData> players, int matchID = -1)
        {
            List<string> collected = new List<string>();

            if (matchID == -1)
                foreach (PlayerData pd in players)
                    foreach (int match in pd.collected.Keys)
                        foreach (string key in pd.collected[match].Keys)
                            collected.Add(key);
            else
                foreach (PlayerData pd in players)
                    if (pd.collected.ContainsKey(matchID))
                        foreach (string key in pd.collected[matchID].Keys)
                            collected.Add(key);

            return collected.Distinct().ToList();
        }

        public static void writeCSVfromStrings(this List<string> lines, List<string> headers, string filename)
        {
            StreamWriter sw = new StreamWriter(filename, false);

            //Write header
            string headerLine = "Steam ID,Steam Name,";
            foreach (string header in headers)
            {
                headerLine += header + ",";
            }

            sw.WriteLine(headerLine.Substring(0, headerLine.Length - 1));

            //Write data
            foreach (string line in lines)
            {
                sw.WriteLine(line);
            }

            sw.Close();
        }


    }



    public class Collector
    {
        //Settings
        public string STEAM_API_KEY;
        public string TARGET_FOLDER;
        public bool ALLTHEDATA = true;

        //Runtime
        private List<PlayerData> allPlayers = new List<PlayerData>();

        public delegate void OnEventSubscription(EventSubscriptionEventArgs e);
        public event OnEventSubscription EventSubscription;

        private RecorderSettings currentRS;

        public Collector(string targetFolder, string apikey)
        {
            this.STEAM_API_KEY = apikey;
            this.TARGET_FOLDER = targetFolder;
        }

        public void pushData(Player p, string setting, int value)
        {
            allPlayers.appendValue(p, currentRS, setting, value);
        }

        public void attachPlayerTimers()
        {
            this.EventSubscription += (EventSubscriptionEventArgs ev) =>
            {
                ev.parser.TickDone += (object sender, TickDoneEventArgs e) =>
                {
                    foreach (Player p in ev.parser.PlayingParticipants)
                    {
                        pushData(p, "Ticks", 1);
                    }

                    foreach (Player p in ev.parser.Participants)
                    {
                        pushData(p, "Ticks on Server", 1);
                    }
                };
            };
        }

        public void attachKillCounters()
        {
            this.EventSubscription += (EventSubscriptionEventArgs ev) =>
            {
                ev.parser.PlayerKilled += (object sender, PlayerKilledEventArgs e) =>
                {
                    pushData(e.Killer, "Kills", 1);

                    if (e.Headshot)
                        pushData(e.Killer, "Headshots", 1);

                    pushData(e.Victim, "Deaths", 1);

                    if (e.Assister != null)
                        pushData(e.Assister, "Assists", 1);

                    if (e.Weapon.Class == EquipmentClass.Grenade)
                        pushData(e.Killer, "Grenade Kills", 1);


                    if (ALLTHEDATA)
                        pushData(e.Killer, e.Weapon.Weapon + " Kills", 1);
                };
            };
        }

        public void attachAll()
        {
            this.EventSubscription += (EventSubscriptionEventArgs ev) =>
            {
                attachPlayerTimers();
                attachKillCounters();

                ev.parser.WeaponFired += (object sender, WeaponFiredEventArgs e) =>
                {
                    pushData(e.Shooter, "Shots", 1);
                };

                ev.parser.SmokeNadeStarted += (object sender, SmokeEventArgs e) => { pushData(e.ThrownBy, "Smokes", 1); };
                ev.parser.FlashNadeExploded += (object sender, FlashEventArgs e) => { pushData(e.ThrownBy, "Flashes", 1); pushData(e.ThrownBy,"Flashed Players", e.FlashedPlayers.Length); };
                ev.parser.ExplosiveNadeExploded += (object sender, GrenadeEventArgs e) => { pushData(e.ThrownBy, "Grenades", 1); };
                ev.parser.FireNadeStarted += (object sender, FireEventArgs e) => { pushData(e.ThrownBy, "Fires", 1); };

                ev.parser.BombPlanted += (object sender, BombEventArgs e) => { pushData(e.Player, "Bomb plants", 1); };
                ev.parser.BombDefused += (object sender, BombEventArgs e) => { pushData(e.Player, "Bomb defuses", 1); };
            };
        }

        public void Process()
        {

#region detect demos
            string[] demos;
            demos = System.IO.Directory.GetFiles(System.Environment.CurrentDirectory + "/" + TARGET_FOLDER + "/", "*.dem", System.IO.SearchOption.AllDirectories);

            Debug.Success("Found {0} demo files", demos.Count());


            for (int i = 0; i < demos.Count();)
            { //                                                                                        KB     MB
                Debug.Blue("{0} - {1}mb\t", Path.GetFileName(demos[i]), new FileInfo(demos[i]).Length / 1024 / 1024);
                i++;

                if (i % 3 == 0)
                    Console.Write("\n");
            }

            Console.Write("\n\n");

            Debug.Info("Press enter to start processing");

            Console.ReadLine();

            #endregion

#region collect match structure
            //Doing the processing
            Dictionary<int, string> matches = new Dictionary<int, string>();
            int mId = 0;
            foreach (string mPath in demos)
            {
                matches.Add(mId, demos[mId]);
                mId++;
            }
            #endregion

#region process all demos
            //Now for each demo
            foreach (int matchID in matches.Keys)
            {
                //Debug.Log("Starting processing match id {0}, demo: {1}", matchID, Path.GetFileName(demos[matchID]));
                Debug.progressBar(matchID + "/" + demos.Count() + "  |  " + Path.GetFileName(demos[matchID]), 0);

                Dictionary<int, long> playerLookups = new Dictionary<int, long>();

                //Set up recorder settings
                RecorderSettings rs = new RecorderSettings();
                rs.matchID = matchID;
                rs.playerLookups = playerLookups;

                currentRS = rs;

                //Create the parser
                DemoParser dp = new DemoParser(File.OpenRead(matches[matchID]));

                dp.ParseHeader();

                //Trigger subscription event
                EventSubscription?.Invoke(new EventSubscriptionEventArgs(dp));


                //Hard coded necessary event handlers ---------------------------------------------------
                dp.PlayerBind += (object sender, PlayerBindEventArgs e) =>
                {
                    if (!playerLookups.ContainsKey(e.Player.EntityID))
                        if (e.Player.SteamID != 0)
                            playerLookups.Add(e.Player.EntityID, e.Player.SteamID);
                };

                int tickCounter = 0;
                dp.TickDone += (object sender, TickDoneEventArgs e) =>
                {
                    tickCounter++;

                    if (tickCounter > 1000)
                    {
                        tickCounter = 0;

                        Debug.updateProgressBar((int)(dp.ParsingProgess * 100));
                    }
                };
                //----------------------------------------------------------------------------------------




                //End of event handlers

                try
                {
                    dp.ParseToEnd();
                }
                catch
                {
                    Debug.exitProgressBar();
                    Debug.Error("Attempted to read past end of stream...");
                }

                //Output per-game csv data
                List<string> headers = allPlayers.getAllHeaders(matchID);
                List<string> outputLines = new List<string>();

                foreach (PlayerData mPlayerDat in allPlayers)
                {
                    if (mPlayerDat.collected.ContainsKey(matchID))
                    {
                        outputLines.Add(mPlayerDat.collected[matchID].getAttribCSVrow(headers, mPlayerDat.s_steamid));
                    }
                }

                if (!Directory.Exists("matches"))
                    Directory.CreateDirectory("matches");

                string csvfile = "matches/ID" + matchID.ToString() + "-" + Path.GetFileNameWithoutExtension(matches[matchID]) + ".csv";

                outputLines.writeCSVfromStrings(headers, csvfile);

                Debug.exitProgressBar();

                //Debug.Success("Demo {0} complete! CSV: {1} ", matchID, csvfile);
            }

            #endregion


            Debug.Success("Finished!");
            Debug.Info("Collecting steam usernames from ID's");
            List<long> steamIDS = new List<long>();
            foreach (PlayerData mPlayerDat in allPlayers)
                steamIDS.Add(mPlayerDat.s_steamid);

            Dictionary<long, string> steamUnameLookup = getSteamUserNamesLookupTable(steamIDS, STEAM_API_KEY);

            Debug.Info("Generating full CSV data...");

            //Output Final FULL CSV data
            List<string> final_headers = allPlayers.getAllHeaders();
            List<string> final_outputLines = new List<string>();

            foreach (PlayerData mPlayerDat in allPlayers)
            {
                string name = "UNKOWN";
                if (steamUnameLookup.ContainsKey(mPlayerDat.s_steamid))
                    name = steamUnameLookup[mPlayerDat.s_steamid];

                final_outputLines.Add(mPlayerDat.getTotalData().getAttribCSVrow(final_headers, mPlayerDat.s_steamid, name));
            }

            string final_csvfile = Guid.NewGuid().ToString("N") + "-total.csv";

            final_outputLines.writeCSVfromStrings(final_headers, final_csvfile);

            Debug.Success("Complete!!!");

            Debug.Info("Press enter to exit...");
            Console.ReadLine();


        }

        public static Dictionary<long, string> getSteamUserNamesLookupTable(List<long> IDS, string steam_api_key)
        {
            string method = "http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/";

            string idsList = "";
            foreach (long id in IDS)
                idsList += id.ToString() + "_";

            STEAM_RootPlayerObject players = new STEAM_RootPlayerObject();

            Debug.Info("Calling steam " + method);
            try
            {
                players = JsonConvert.DeserializeObject<STEAM_RootPlayerObject>(request.GET(method + "?key=" + steam_api_key + "&steamids=" + idsList));
                Debug.Success("Steam returned successfully!");
            }
            catch
            {
                Debug.Error("Unable to fetch steam info correctly...");
            }


            Dictionary<long, string> output = new Dictionary<long, string>();

            foreach (STEAM_Player player in players.response.players)
                output.Add(Convert.ToInt64(player.steamid), player.personaname);

            return output;
        }
    }

    public class EventSubscriptionEventArgs : EventArgs
    {
        public DemoParser parser { get; }

        public EventSubscriptionEventArgs(DemoParser parser)
        {
            this.parser = parser;
        }
    }
}
