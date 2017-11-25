using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using TopStatsWaffle.Serialization;
using DemoInfo;

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


            // PLAYER EVENTS ===================================================
            dp.PlayerKilled += (object sender, PlayerKilledEventArgs e) => {
                md.addEvent(typeof(PlayerKilledEventArgs), e);
            };

            // BOMB EVENTS =====================================================
            dp.BombPlanted += (object sender, BombEventArgs e) => {
                md.addEvent(typeof(BombDefuseEventArgs), e);
            };

            dp.BombDefused += (object sender, BombEventArgs e) => {
                md.addEvent(typeof(BombEventArgs), e);
            };

            // WEAPON EVENTS ===================================================
            dp.WeaponFired += (object sender, WeaponFiredEventArgs e) => {
                md.addEvent(typeof(WeaponFiredEventArgs), e);
            };

            // GRENADE EVENTS ==================================================
            dp.ExplosiveNadeExploded += (object sender, GrenadeEventArgs e) => {
                md.addEvent(typeof(GrenadeEventArgs), e);
            };

            dp.FireNadeStarted += (object sender, FireEventArgs e) => {
                md.addEvent(typeof(NadeEventArgs), e);
            };

            dp.SmokeNadeStarted += (object sender, SmokeEventArgs e) => {
                md.addEvent(typeof(SmokeEventArgs), e);
            };

            dp.FlashNadeExploded += (object sender, FlashEventArgs e) => {
                md.addEvent(typeof(FlashEventArgs), e);
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

        public void SaveCSV(string path, Dictionary<string, IEnumerable<Player>> values, bool writeTicks = true)
        {
            StreamWriter sw = new StreamWriter(path, false);

            //Write header
            string header = "SteamID,";

            foreach(string catagory in values.Keys)
            {
                header += catagory + ",";
            }

            if(writeTicks)
                header += "Ticks Alive,Ticks on Server,Ticks playing,";

            sw.WriteLine(header.Substring(0, header.Length - 1));

            Dictionary<long, Dictionary<string, long>> data = new Dictionary<long, Dictionary<string, long>>();

            foreach(string catagory in values.Keys)
            {
                foreach(Player p in values[catagory])
                {
                    //Skip players not in this catagory
                    if (p == null)
                        continue;

                    //Add player to collections list if doesnt exist
                    if (!data.ContainsKey(playerLookups[p.EntityID]))
                        data.Add(playerLookups[p.EntityID], new Dictionary<string, long>());

                    //Add catagory to dictionary if doesnt exist
                    if (!data[playerLookups[p.EntityID]].ContainsKey(catagory))
                        data[playerLookups[p.EntityID]].Add(catagory, 0);

                    //Increment it
                    data[playerLookups[p.EntityID]][catagory]++;
                }
            }

            foreach (long player in data.Keys)
            {
                string playerLine = player + ",";

                foreach (string catagory in values.Keys)
                {
                    if (data[player].ContainsKey(catagory))
                        playerLine += data[player][catagory] + ",";
                    else
                        playerLine += "0,";
                }

                if (writeTicks)
                {
                    if (playerLookups.ContainsValue(player))
                    {
                        foreach (int entid in playerLookups.Keys)
                        {
                            if (playerLookups[entid] == player)
                            {
                                playerLine += this.playerTicks[entid].ticksAlive + ",";
                                playerLine += this.playerTicks[entid].ticksOnServer + ",";
                                playerLine += this.playerTicks[entid].ticksPlaying + ",";
                                break;
                            }
                        }
                    }
                    else
                    {
                        playerLine += "0,0,0,";
                    }
                }

                sw.WriteLine(playerLine.Substring(0, playerLine.Length - 1));
            }

            sw.Close();
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