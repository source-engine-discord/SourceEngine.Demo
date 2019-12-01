using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SourceEngine.Demo.Stats
{
    public class STEAM_Player
    {
        public string steamid { get; set; }
        public int communityvisibilitystate { get; set; }
        public int profilestate { get; set; }
        public string personaname { get; set; }
        public int lastlogoff { get; set; }
        public string profileurl { get; set; }
        public string avatar { get; set; }
        public string avatarmedium { get; set; }
        public string avatarfull { get; set; }
        public int personastate { get; set; }
        public string realname { get; set; }
        public string primaryclanid { get; set; }
        public int timecreated { get; set; }
        public int personastateflags { get; set; }
        public string loccountrycode { get; set; }
        public string locstatecode { get; set; }
        public int loccityid { get; set; }
    }

    public class STEAM_Response
    {
        public List<STEAM_Player> players { get; set; }
    }

    public class STEAM_RootPlayerObject
    {
        public STEAM_Response response { get; set; }
    }

    public static class Steam
    {
        private static string api_key;

        public static void setAPIKey(string key)
        {
            api_key = key;
        }

        public static Dictionary<long, string> getSteamUserNamesLookupTable(List<long> IDS)
        {
            string method = "http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/";

            string idsList = "";
            foreach (long id in IDS)
                idsList += id.ToString() + "_";

            STEAM_RootPlayerObject players = new STEAM_RootPlayerObject();

            Debug.Info("Calling steam " + method);
            try
            {
                players = JsonConvert.DeserializeObject<STEAM_RootPlayerObject>(request.GET(method + "?key=" + api_key + "&steamids=" + idsList));
                Debug.Success("Steam returned successfully!");
            }
            catch
            {
                Debug.Error("Unable to fetch steam info correctly...");
                return null;
            }

            Dictionary<long, string> output = new Dictionary<long, string>();

            foreach (STEAM_Player player in players.response.players)
                output.Add(Convert.ToInt64(player.steamid), player.personaname);

            return output;
        }
    }
}
