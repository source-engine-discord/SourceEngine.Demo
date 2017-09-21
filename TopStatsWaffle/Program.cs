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
    /*
    public class PlayerData
    {
        public long s_steamid;
        public string s_steamname;

        public Dictionary<int, Dictionary<string, int>> collected = new Dictionary<int, Dictionary<string, int>>(); 
         
        public PlayerData(long steamid, string name ="")
        {
            this.s_steamid = steamid;
            this.s_steamname = name;
        }

        public Dictionary<string, int> getTotalData()
        {
            Dictionary<string, int> total = new Dictionary<string, int>();

            //Iterate each matchkey
            foreach(int matchKey in collected.Keys)
            {
                //Iterate each key in the collected keyss 
                foreach(string attribKey in collected[matchKey].Keys)
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
            
            if(matchID == -1)
                foreach (PlayerData pd in players)
                    foreach (int match in pd.collected.Keys)
                        foreach (string key in pd.collected[match].Keys)
                            collected.Add(key);
            else
                foreach (PlayerData pd in players)
                    if(pd.collected.ContainsKey(matchID))
                        foreach (string key in pd.collected[matchID].Keys)
                            collected.Add(key);

            return collected.Distinct().ToList();
        }

        public static void writeCSVfromStrings(this List<string> lines, List<string> headers, string filename)
        {
            StreamWriter sw = new StreamWriter(filename, false);

            //Write header
            string headerLine = "Steam ID,Steam Name,";
            foreach(string header in headers)
            {
                headerLine += header + ",";
            }

            sw.WriteLine(headerLine.Substring(0, headerLine.Length - 1));

            //Write data
            foreach(string line in lines)
            {
                sw.WriteLine(line);
            }

            sw.Close();
        }
    }*/

    class Program
    {


        static string STEAM_API_KEY = "";

        static void Main(string[] args)
        {
            if(!File.Exists("config.cfg"))
            {
                Debug.Error("config.cfg cannot be found!!!");

                Debug.Info("Creating one.");
                StreamWriter sw = new StreamWriter("config.cfg");

                Debug.Blue("Enter Steam API key: ");
                string apiKey = Console.ReadLine();

                sw.WriteLine(apiKey);

                sw.Close();

                Debug.Success("Finished writing config...");
            }

            StreamReader sr = new StreamReader("config.cfg");
            STEAM_API_KEY = sr.ReadLine();
            sr.Close();

            if(STEAM_API_KEY == null || STEAM_API_KEY == "")
            {
                Debug.Error("STEAM KEY NOT SUPPLIED. Press enter to exit...");
                Console.ReadLine();
                return;
            }

            Collector c = new Collector("demos", STEAM_API_KEY);
            c.Process();

            
        }


    }
}
