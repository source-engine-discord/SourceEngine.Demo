using System;
using System.Collections.Generic;
using System.Linq;

using SourceEngine.Demo.Parser;

namespace SourceEngine.Demo.Stats
{
    internal static class EXTMethods
    {
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
    }
}
