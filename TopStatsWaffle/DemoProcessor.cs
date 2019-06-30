using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using TopStatsWaffle.Serialization;
using DemoInfo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TopStatsWaffle.Models;

namespace TopStatsWaffle
{
    enum PSTATUS
    {
        ONSERVER,
        PLAYING,
        ALIVE
    }

    class TickCounter
    {
        public long ticksOnServer = 0;
        public long ticksPlaying = 0;
        public long ticksAlive = 0;

        public string detectedName = "NOT FOUND";
    }

    public class PlayerWeapon
    {
        public string name;
    }

    class MatchData
    {
        public Dictionary<Type, List<object>> events = new Dictionary<Type, List<object>>();

        public Dictionary<int, TickCounter> playerTicks = new Dictionary<int, TickCounter>();
        public Dictionary<int, long> playerLookups = new Dictionary<int, long>();

        public bool passed = false;

        void addEvent(Type type, object ev)
        {
            //Create if doesnt exist
            if (!this.events.ContainsKey(type))
                this.events.Add(type, new List<object>());

            events[type].Add(ev);
        }

        void bindPlayer(Player p)
        {
            if (!playerTicks.ContainsKey(p.EntityID))
                playerTicks.Add(p.EntityID, new TickCounter());

            if (!playerLookups.ContainsKey(p.EntityID))
                if(p.SteamID != 0)
                    playerLookups.Add(p.EntityID, p.SteamID);

            if (playerTicks[p.EntityID].detectedName == "NOT FOUND" && p.Name != "" && p.Name != null)
                playerTicks[p.EntityID].detectedName = p.Name;
        }

        void addTick(Player p, PSTATUS status)
        {
            bindPlayer(p);

            if(status == PSTATUS.ONSERVER)
                playerTicks[p.EntityID].ticksOnServer++;

            if (status == PSTATUS.ALIVE)
                playerTicks[p.EntityID].ticksAlive++;

            if (status == PSTATUS.PLAYING)
                playerTicks[p.EntityID].ticksPlaying++;
        }

        public static MatchData fromDemoFile(string file)
        {
            MatchData md = new MatchData();

            //Create demo parser instance
            DemoParser dp = new DemoParser(File.OpenRead(file));

            dp.ParseHeader();

            dp.PlayerBind += (object sender, PlayerBindEventArgs e) => {
                md.bindPlayer(e.Player);
            };

            // SERVER EVENTS ===================================================
            dp.RoundEnd += (object sender, RoundEndedEventArgs e) =>
            {
                md.addEvent(typeof(RoundEndedEventArgs), e);
            };

            // PLAYER EVENTS ===================================================
            dp.PlayerKilled += (object sender, PlayerKilledEventArgs e) => {
                md.addEvent(typeof(PlayerKilledEventArgs), e);
            };
            
            dp.RoundMVP += (object sender, RoundMVPEventArgs e) => {
                md.addEvent(typeof(RoundMVPEventArgs), e);
            };

            // BOMB EVENTS =====================================================
            dp.BombPlanted += (object sender, BombEventArgs e) => {
                md.addEvent(typeof(BombEventArgs), e);
            };

            dp.BombDefused += (object sender, BombEventArgs e) => {
                md.addEvent(typeof(BombDefuseEventArgs), e);
            };

            // WEAPON EVENTS ===================================================
            dp.WeaponFired += (object sender, WeaponFiredEventArgs e) => {
                md.addEvent(typeof(WeaponFiredEventArgs), e);
            };

            // GRENADE EVENTS ==================================================
            dp.ExplosiveNadeExploded += (object sender, GrenadeEventArgs e) => {
                md.addEvent(typeof(GrenadeEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.FireNadeStarted += (object sender, FireEventArgs e) => {
                md.addEvent(typeof(FireEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.SmokeNadeStarted += (object sender, SmokeEventArgs e) => {
                md.addEvent(typeof(SmokeEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.FlashNadeExploded += (object sender, FlashEventArgs e) => {
                md.addEvent(typeof(FlashEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.DecoyNadeStarted += (object sender, DecoyEventArgs e) => {
                md.addEvent(typeof(DecoyEventArgs), e);
                md.addEvent(typeof(NadeEventArgs), e);
            };

            // PLAYER TICK HANDLER ============================================
            dp.TickDone += (object sender, TickDoneEventArgs e) => {
                foreach(Player p in dp.PlayingParticipants){
                    md.addTick(p, PSTATUS.PLAYING);

                    if (p.IsAlive)
                        md.addTick(p, PSTATUS.ALIVE);
                }

                foreach(Player p in dp.Participants){
                    if(!p.Disconnected)
                        md.addTick(p, PSTATUS.ONSERVER);
                }
            };

            const int interval = 2500;
            int progMod = interval;

            ProgressViewer pv = new ProgressViewer(Path.GetFileName(file));

            // PROGRESS BAR ==================================================
            dp.TickDone += (object sender, TickDoneEventArgs e) =>
            {
                progMod++;
                if (progMod >= interval)
                {
                    progMod = 0;

                    pv.percent = dp.ParsingProgess;
                    pv.Draw();
                }
            };

            try
            {
                dp.ParseToEnd();
                pv.End();

                md.passed = true;
            }
            catch
            {
                pv.Error();
            }

            dp.Dispose();
            return md;
        }

        public void SaveCSV(
            string newFilepath, List<string> demo, Dictionary<string, IEnumerable<Player>> playerValues, Dictionary<string, IEnumerable<char>> bombsiteValues,
            Dictionary<string, IEnumerable<Team>> teamValues, Dictionary<string, IEnumerable<RoundEndReason>> roundEndReasonValues, Dictionary<string, IEnumerable<NadeEventArgs>> grenadeValues, bool writeTicks = true
        )
        {
            string path = newFilepath += ".csv";
            if (File.Exists(path))
                File.Delete(path);

            StreamWriter sw = new StreamWriter(path, false);

            /* map info */
            MapInfo mapInfo = new MapInfo() { MapName = demo[1], Date = demo[2], TestType = demo[3] };

            string header = "Mapname,Date,Test Type";
            string[] headerSplit = header.Split(',');

            sw.WriteLine(header);
            sw.WriteLine($"{ demo[1] },{ demo[2] },{ demo[3] }");
            /* map info end */

            /* player stats */
            List<PlayerStats> playerStats = new List<PlayerStats>();

            sw.WriteLine(string.Empty);

            header = "Player Name,SteamID,";

            foreach(string catagory in playerValues.Keys)
            {
                header += catagory + ",";
            }

            if(writeTicks)
                header += "Ticks Alive,Ticks on Server,Ticks playing,";

            sw.WriteLine(header.Substring(0, header.Length - 1));

            Dictionary<long, Dictionary<string, long>> data = new Dictionary<long, Dictionary<string, long>>();
            Dictionary<long, Dictionary<string, string>> playerNames = new Dictionary<long, Dictionary<string, string>>();

            foreach (string catagory in playerValues.Keys)
            {
                foreach(Player p in playerValues[catagory])
                {
                    //Skip players not in this catagory
                    if (p == null)
                        continue;

                    if (!playerLookups.ContainsKey(p.EntityID))
                        continue;

                    //Add player to collections list if doesnt exist
                    if (!playerNames.ContainsKey(playerLookups[p.EntityID]))
                        playerNames.Add(playerLookups[p.EntityID], new Dictionary<string, string>());

                    if (!data.ContainsKey(playerLookups[p.EntityID]))
                        data.Add(playerLookups[p.EntityID], new Dictionary<string, long>());

                    //Add catagory to dictionary if doesnt exist
                    if (!playerNames[playerLookups[p.EntityID]].ContainsKey("Name"))
                        playerNames[playerLookups[p.EntityID]].Add("Name", p.Name);

                    if (!data[playerLookups[p.EntityID]].ContainsKey(catagory))
                        data[playerLookups[p.EntityID]].Add(catagory, 0);

                    //Increment it
                    data[playerLookups[p.EntityID]][catagory]++;
                }
            }

            int counter = 0;
            foreach (long player in data.Keys)
            {
                var match = playerNames.Where(p => p.Key.ToString() == player.ToString());
                var playerName = match.ElementAt(0).Value.ElementAt(0).Value;
                var steamID = match.ElementAt(0).Key;

                string playerLine = $"{ playerName },{ player },";

                List<int> statsList1 = new List<int>();
                foreach (string catagory in playerValues.Keys)
                {
                    if (data[player].ContainsKey(catagory))
                    {
                        playerLine += data[player][catagory] + ",";
                        statsList1.Add((int)data[player][catagory]);
                    }
                    else
                    {
                        playerLine += "0,";
                        statsList1.Add(0);
                    }
                }

                List<long> statsList2 = new List<long>();
                if (writeTicks)
                {
                    if (playerLookups.ContainsValue(player))
                    {
                        foreach (int entid in playerLookups.Keys)
                        {
                            if (playerLookups[entid] == player)
                            {
                                playerLine += this.playerTicks[entid].ticksAlive + ",";
                                statsList2.Add(this.playerTicks[entid].ticksAlive);

                                playerLine += this.playerTicks[entid].ticksOnServer + ",";
                                statsList2.Add(this.playerTicks[entid].ticksOnServer);

                                playerLine += this.playerTicks[entid].ticksPlaying + ",";
                                statsList2.Add(this.playerTicks[entid].ticksPlaying);

                                break;
                            }
                        }
                    }
                    else
                    {
                        playerLine += "0,0,0,";
                    }
                }

                string[] stats = playerLine.Split(',');

                playerStats.Add(new PlayerStats() {
                    PlayerName = playerName,
                    SteamID = steamID,
                    Deaths = statsList1.ElementAt(0),
                    Kills = statsList1.ElementAt(1),
                    Headshots = statsList1.ElementAt(2),
                    Assists = statsList1.ElementAt(3),
                    MVPs = statsList1.ElementAt(4),
                    Shots = statsList1.ElementAt(5),
                    Plants = statsList1.ElementAt(6),
                    Defuses = statsList1.ElementAt(7),
                    TicksAlive = statsList2.ElementAt(0),
                    TicksOnServer = statsList2.ElementAt(1),
                    TicksPlaying = statsList2.ElementAt(2),
                });

                sw.WriteLine(playerLine.Substring(0, playerLine.Length - 1));

                counter++;
            }
            /* player stats end */

            /* winning team stats, round wins team and reason stats */
            WinnersStats winnersStats;
            List<RoundsStats> roundsStats = new List<RoundsStats>();

            sw.WriteLine(string.Empty);

            const string tName = "Terrorist", ctName = "CounterTerrorist";
            const string tKills = "TerroristWin", ctKills = "CTWin", bombed = "TargetBombed", defused = "BombDefused", timeout = "TargetSaved";

            var roundsWonTeams = teamValues["RoundsWonTeams"].ToList();
            roundsWonTeams.RemoveAll(r => !r.ToString().Equals(tName)
                                       && !r.ToString().Equals(ctName)
            );

            var roundsWonReasons = roundEndReasonValues["RoundsWonReasons"].ToList();
            roundsWonReasons.RemoveAll(r => !r.ToString().Equals(tKills)
                                         && !r.ToString().Equals(ctKills)
                                         && !r.ToString().Equals(bombed)
                                         && !r.ToString().Equals(defused)
                                         && !r.ToString().Equals(timeout)
            );

            int roundsUntilSwapSides = Convert.ToInt32(roundsWonTeams.Count() / 2);

            int numRoundsWonTeamA = roundsWonTeams.Take(roundsUntilSwapSides).Where(r => r.ToString().Equals(tName)).Count()
                                  + roundsWonTeams.Skip(roundsUntilSwapSides).Where(r => r.ToString().Equals(ctName)).Count();

            int numRoundsWonTeamB = roundsWonTeams.Take(roundsUntilSwapSides).Where(r => r.ToString().Equals(ctName)).Count()
                                  + roundsWonTeams.Skip(roundsUntilSwapSides).Where(r => r.ToString().Equals(tName)).Count();

            string winningTeam = (numRoundsWonTeamA >= numRoundsWonTeamB) ? (numRoundsWonTeamA > numRoundsWonTeamB) ? "Team Alpha" : "Draw" : "Team Bravo";

            header = "Winning Team, Team Alpha Rounds, Team Bravo Rounds";
            sw.WriteLine(header);
            sw.WriteLine($"{ winningTeam },{ numRoundsWonTeamA },{ numRoundsWonTeamB }");

            winnersStats = new WinnersStats() { WinningTeam = winningTeam, TeamAlphaRounds = numRoundsWonTeamA, TeamBetaRounds = numRoundsWonTeamB };

            sw.WriteLine(string.Empty);

            header = "Round,Half,Winners,Win Method";
            sw.WriteLine(header);

            for (int i=0; i < roundsWonTeams.Count(); i++)
            {
                if (roundsWonTeams[i].ToString().Equals(tName) || roundsWonTeams[i].ToString().Equals(ctName))
                {
                    string reason = string.Empty;

                    switch (roundsWonReasons[i].ToString())
                    {
                        case tKills:
                            reason = "T Kills";
                            break;
                        case ctKills:
                            reason = "CT Kills";
                            break;
                        case bombed:
                            reason = "Bombed";
                            break;
                        case defused:
                            reason = "Defused";
                            break;
                        case timeout:
                            reason = "Timeout";
                            break;
                    }

                    string half = i < roundsUntilSwapSides ? "First" : "Second";

                    sw.WriteLine($"Round{ i },{ half },{ roundsWonTeams[i].ToString() },{ reason }");
                    roundsStats.Add(new RoundsStats() { Round = $"Round{ i }", Winners = roundsWonTeams[i].ToString(), WinMethod = reason });
                    }
            }
            /* winning team stats, round wins team and reason stats end */

            /* bombsite stats */
            List<BombsiteStats> bombsiteStats = new List<BombsiteStats>();

            sw.WriteLine(string.Empty);

            List<char> bombsitePlants = new List<char>(bombsiteValues.ElementAt(0).Value);
            List<char> bombsiteDefuses = new List<char>(bombsiteValues.ElementAt(1).Value);

            header = "Bombsite,Plants,Defuses";
            sw.WriteLine(header);

            sw.WriteLine($"A,{ bombsitePlants.Where(b => b.ToString().Equals("A")).Count() },{ bombsiteDefuses.Where(b => b.ToString().Equals("A")).Count() }");
            sw.WriteLine($"B,{ bombsitePlants.Where(b => b.ToString().Equals("B")).Count() },{ bombsiteDefuses.Where(b => b.ToString().Equals("B")).Count() }");

            bombsiteStats.Add(new BombsiteStats() { Bombsite = 'A', Plants = bombsitePlants.Where(b => b.ToString().Equals("A")).Count(), Defuses = bombsiteDefuses.Where(b => b.ToString().Equals("A")).Count() });
            bombsiteStats.Add(new BombsiteStats() { Bombsite = 'B', Plants = bombsitePlants.Where(b => b.ToString().Equals("B")).Count(), Defuses = bombsiteDefuses.Where(b => b.ToString().Equals("B")).Count() });
            /* bombsite stats end */

            /* Grenades total stats */
            List<GrenadesTotalStats> grenadesTotalStats = new List<GrenadesTotalStats>();

            sw.WriteLine(string.Empty);

            string[] nadeTypes = { "Flash", "Smoke", "HE", "Incendiary", "Decoy" };

            List<NadeEventArgs> nades = new List<NadeEventArgs>(grenadeValues.ElementAt(0).Value);
            var flashes = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[0]));
            var smokes = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[1]));
            var hegrenades = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[2]));
            var incendiaries = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[3]));
            var decoys = nades.Where(f => f.NadeType.ToString().Equals(nadeTypes[4]));

            List<IEnumerable<NadeEventArgs>> nadeGroups = new List<IEnumerable<NadeEventArgs>>() { flashes, smokes, hegrenades, incendiaries, decoys };

            header = "Nade Type,Amount Used";
            sw.WriteLine(header);

            for (int i=0; i < nadeTypes.Count(); i++)
            {
                sw.WriteLine($"{ nadeTypes[i] },{ nadeGroups.ElementAt(i).Count() }");

                grenadesTotalStats.Add(new GrenadesTotalStats() { NadeType = nadeTypes[i], AmountUsed = nadeGroups.ElementAt(i).Count() });
            }
            /* Grenades total stats end */

            /* Grenades specific stats */
            List<GrenadesSpecificStats> grenadesSpecificStats = new List<GrenadesSpecificStats>();

            sw.WriteLine(string.Empty);

            header = "Nade Type,SteamID,X Position,Y Position,Z Position,Num Players Flashed";

            sw.WriteLine(header);

            foreach (var nadeGroup in nadeGroups)
            {
                if (nadeGroup.Count() > 0)
                {
                    bool flashGroup = nadeGroup.ElementAt(0).NadeType.ToString() == nadeTypes[0] ? true : false; //check if in flash group

                    foreach (var nade in nadeGroup)
                    {
                        string[] positionSplit = nade.Position.ToString().Split(new string[] { "{X: ", ", Y: ", ", Z: ", "}" }, StringSplitOptions.None);
                        string positions = $"{ positionSplit[1] },{ positionSplit[2] },{ positionSplit[3] }";


                        if (flashGroup)
                        {
                            var flash = nade as FlashEventArgs;
                            int numOfPlayersFlashed = flash.FlashedPlayers.Count();

                            sw.WriteLine($"{ nade.NadeType.ToString() },{ nade.ThrownBy.SteamID.ToString() },{ positions },{ numOfPlayersFlashed }");

                            grenadesSpecificStats.Add(new GrenadesSpecificStats() { NadeType = nade.NadeType.ToString(), SteamID = nade.ThrownBy.SteamID, XPosition = positions[0], YPosition = positions[1], ZPosition = positions[2], NumPlayersFlashed = numOfPlayersFlashed });
                        }
                        else
                        {
                            sw.WriteLine($"{ nade.NadeType.ToString() },{ nade.ThrownBy.SteamID.ToString() },{ positions }");

                            grenadesSpecificStats.Add(new GrenadesSpecificStats() { NadeType = nade.NadeType.ToString(), SteamID = nade.ThrownBy.SteamID, XPosition = positions[0], YPosition = positions[1], ZPosition = positions[2] });
                        }
                    }
                }
            }
            /* Grenades specific stats end */

            sw.Close();

            /* JSON creation */
            path = newFilepath += ".json";
            if (File.Exists(path))
                File.Delete(path);

            StreamWriter sw2 = new StreamWriter(path, false);

            AllStats allStats = new AllStats() {
                MapInfo = mapInfo,
                PlayerStats = playerStats,
                WinnersStats = winnersStats,
                RoundsStats = roundsStats,
                BombsiteStats = bombsiteStats,
                GrenadesTotalStats = grenadesTotalStats,
                GrenadesSpecificStats = grenadesSpecificStats,
            };
            
            string json = JsonConvert.SerializeObject(new
            {
                mapInfo,
                playerStats,
                winnersStats,
                roundsStats,
                bombsiteStats,
                grenadesTotalStats,
                grenadesSpecificStats,
            },
                Formatting.Indented
            );

            sw2.WriteLine(json);
            /* JSON creation end*/

            sw2.Close();
        }

        public IEnumerable<object> selectWeaponsEventsByName(string name)
        {
            var shots = (from shot in getEvents<WeaponFiredEventArgs>()
                           where (shot as WeaponFiredEventArgs).Weapon.Weapon.ToString() == name
                           select shot);

            return shots;
        }

        public List<object> getEvents<T>()
        {
            Type t = typeof(T);

            if(this.events.ContainsKey(t))
                return this.events[t];

            return new List<object>();
        }
    }
}