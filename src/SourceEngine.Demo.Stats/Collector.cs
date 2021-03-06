using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using SourceEngine.Demo.Parser;

namespace SourceEngine.Demo.Stats
{
    public class PlayerData
    {
        public Dictionary<int, Dictionary<string, int>> collected = new();
        public long s_steamid;
        public string s_steamname;

        public PlayerData(long steamid, string name = "")
        {
            s_steamid = steamid;
            s_steamname = name;
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

    internal class RecorderSettings
    {
        public int matchID = 0;
        public Dictionary<int, long> playerLookups = new();
    }

    public class EventSubscriptionEventArgs : EventArgs
    {
        public EventSubscriptionEventArgs(DemoParser parser)
        {
            this.parser = parser;
        }

        public DemoParser parser { get; }
    }

    public class Collector
    {
        public delegate void OnEventSubscription(EventSubscriptionEventArgs e);

        //Runtime
        private readonly List<PlayerData> allPlayers = new();

        private RecorderSettings currentRS;

        //Settings
        public string TARGET_FOLDER;

        public Collector(string targetFolder)
        {
            TARGET_FOLDER = targetFolder;
        }

        public event OnEventSubscription EventSubscription;

        public void pushData(Player p, string key, int value)
        {
            allPlayers.appendValue(p, currentRS, key, value);
        }

        public void attachAll()
        {
            EventSubscription += (EventSubscriptionEventArgs ev) =>
            {
                ev.parser.TickDone += (object sender, TickDoneEventArgs e) =>
                {
                    foreach (Player p in ev.parser.PlayingParticipants)
                        pushData(p, "Ticks", 1);

                    foreach (Player p in ev.parser.Participants)
                        pushData(p, "Ticks on Server", 1);
                };

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

                    pushData(e.Killer, e.Weapon.Weapon + " Kills", 1);
                };

                ev.parser.WeaponFired += (object sender, WeaponFiredEventArgs e) =>
                {
                    pushData(e.Shooter, "Shots", 1);
                };

                ev.parser.RoundMVP += (object sender, RoundMVPEventArgs e) => { pushData(e.Player, "MVPs", 1); };

                ev.parser.SmokeNadeStarted += (object sender, SmokeEventArgs e) =>
                {
                    pushData(e.ThrownBy, "Smokes", 1);
                };

                ev.parser.FlashNadeExploded += (object sender, FlashEventArgs e) =>
                {
                    pushData(e.ThrownBy, "Flashes", 1);
                    pushData(e.ThrownBy, "Flashed Players", e.FlashedPlayers.Length);
                };

                ev.parser.ExplosiveNadeExploded += (object sender, GrenadeEventArgs e) =>
                {
                    pushData(e.ThrownBy, "Grenades", 1);
                };

                ev.parser.FireNadeStarted += (object sender, FireEventArgs e) => { pushData(e.ThrownBy, "Fires", 1); };
                ev.parser.DecoyNadeStarted += (object sender, DecoyEventArgs e) =>
                {
                    pushData(e.ThrownBy, "Decoys", 1);
                };

                ev.parser.BombPlanted += (object sender, BombEventArgs e) => { pushData(e.Player, "Bomb plants", 1); };
                ev.parser.BombDefused += (object sender, BombEventArgs e) => { pushData(e.Player, "Bomb defuses", 1); };
            };
        }

        public void Process()
        {
            #region detect demos

            string[] demos = Directory.GetFiles(
                Environment.CurrentDirectory + "/" + TARGET_FOLDER + "/",
                "*.dem",
                SearchOption.AllDirectories
            );

            Debug.Success("Found {0} demo files", demos.Length);

            for (int i = 0; i < demos.Length;)
            {
                //                                                                                        KB     MB
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
                Debug.progressBar(matchID + "/" + demos.Length + "  |  " + Path.GetFileName(demos[matchID]), 0);

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

                // -------------------------------------------------------------------------------------

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

                dp.Dispose();

                Debug.exitProgressBar();
            }

            #endregion

            Debug.Success("Complete!!!");
        }
    }
}
