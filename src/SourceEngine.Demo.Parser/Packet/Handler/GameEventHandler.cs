using System;
using System.Collections.Generic;
using System.Linq;

using SourceEngine.Demo.Parser.Messages.Fast.Net;

namespace SourceEngine.Demo.Parser.Packet.Handler
{
    /// <summary>
    /// This class manages all GameEvents for a demo-parser.
    /// </summary>
    public static class GameEventHandler
    {
        static List<Player> currentRoundBotTakeovers = new List<Player>();

        private static double timestampFreezetimeEnded = 0; //the total number of seconds passed by the end of the last round
        private static int numOfChickensAliveExpected = 0;


        public static void HandleGameEventList(IEnumerable<GameEventList.Descriptor> gel, DemoParser parser)
        {
            parser.EventDescriptors = new Dictionary<int, GameEventList.Descriptor>();
            foreach (var d in gel)
                parser.EventDescriptors[d.EventId] = d;
        }

        /// <summary>
        /// Counts the number of chickens currently alive
        /// </summary>
        /// <param name="parser">The parser to get the entity list from.</param>
        private static int CountChickensAlive(DemoParser parser)
        {
            int numOfChickensAlive = 0;

            for (int i = 0; i < parser.Entities.Count(); i++)
            {
                if (parser.Entities.ElementAt(i) != null && parser.Entities.ElementAt(i).ServerClass.Name == "CChicken")
                {
                    numOfChickensAlive++;
                }
            }

            return numOfChickensAlive;
        }

        /// <summary>
        /// Apply the specified rawEvent to the parser.
        /// </summary>
        /// <param name="rawEvent">The raw event.</param>
        /// <param name="parser">The parser to mutate.</param>
        public static void Apply(GameEvent rawEvent, DemoParser parser, bool parseChickens)
        {
            int numOfChickensAlive = 0;
            if (parseChickens) // Parse chickens unless explicitly told not to
            {
                numOfChickensAlive = CountChickensAlive(parser); //awkward temporary method of counting the number of chickens as killing a chicken does not seem to trigger the other_death event
            }

            var descriptors = parser.EventDescriptors;
            //previous blind implementation
            var blindPlayers = parser.BlindPlayers;

            if (descriptors == null)
                return;

            Dictionary<string, object> data;
            var eventDescriptor = descriptors[rawEvent.EventId];

            if (parser.Players.Count == 0 && eventDescriptor.Name != "player_connect")
                return;

            if (eventDescriptor.Name == "round_start") {
                data = MapData (eventDescriptor, rawEvent);

                RoundStartedEventArgs rs = new RoundStartedEventArgs () {
                    TimeLimit = (int)data["timelimit"],
                    FragLimit = (int)data["fraglimit"],
                    Objective = (string)data["objective"]
                };

                parser.RaiseRoundStart (rs);

                numOfChickensAliveExpected = numOfChickensAlive; //sets expected number of chickens at start of a new round
            }

            if (eventDescriptor.Name == "cs_win_panel_match")
                parser.RaiseWinPanelMatch();

            if (eventDescriptor.Name == "round_announce_final")
                parser.RaiseRoundFinal();

            if (eventDescriptor.Name == "round_announce_last_round_half")
                parser.RaiseLastRoundHalf();

            // this occurs at the same time as the round_win_reason is decided, NOT directly before freezetime
            if (eventDescriptor.Name == "round_end")
            {
                data = MapData(eventDescriptor, rawEvent);

                Team t = Team.Spectate;

                int winner = (int)data["winner"];

                if (winner == parser.tID)
                    t = Team.Terrorist;
                else if (winner == parser.ctID)
                    t = Team.CounterTerrorist;

                //round length
                double roundLength = parser.CurrentTime - timestampFreezetimeEnded;

                RoundEndedEventArgs roundEnd = new RoundEndedEventArgs()
                {
                    Reason = (RoundEndReason)data["reason"],
                    Winner = t,
                    Message = (string)data["message"],
                    Length = roundLength + 4, //gets overwritten when round_officially_ended event occurs, but is here as a backup incase that event does not trigger, as a backup estimate
                };

                parser.RaiseRoundEnd(roundEnd);
            }

            if (eventDescriptor.Name == "round_officially_ended")
            {
                // resets the list of players that have taken over bots in the round
                currentRoundBotTakeovers = new List<Player>();

                //round length
                double roundLength = parser.CurrentTime - timestampFreezetimeEnded;

                RoundOfficiallyEndedEventArgs roundOfficiallyEnded = new RoundOfficiallyEndedEventArgs()
                {
                    Length = roundLength,
                };

                parser.RaiseRoundOfficiallyEnded(roundOfficiallyEnded);

                numOfChickensAliveExpected = 0; //sets expected number of chickens to 0 until the start of the next round to avoid edge cases
            }
            else if (parseChickens) //checks for killed chickens if required
            {
                while (numOfChickensAlive < numOfChickensAliveExpected)
                {
                    parser.RaiseChickenKilled();
                    numOfChickensAliveExpected--;
                }
            }

            if (eventDescriptor.Name == "announce_phase_end")
                parser.RaiseSwitchSides();

            if (eventDescriptor.Name == "round_mvp") {
                data = MapData (eventDescriptor, rawEvent);

                RoundMVPEventArgs roundMVPEvent = new RoundMVPEventArgs();
                roundMVPEvent.Player = parser.Players.ContainsKey((int)data["userid"]) ? new Player(parser.Players[(int)data["userid"]]) : null;
                roundMVPEvent.Reason = (RoundMVPReason)data["reason"];

                parser.RaiseRoundMVP (roundMVPEvent);
            }

            if (eventDescriptor.Name == "bot_takeover")
            {
                data = MapData(eventDescriptor, rawEvent);

                BotTakeOverEventArgs botTakeOverArgs = new BotTakeOverEventArgs();
                botTakeOverArgs.Taker = parser.Players.ContainsKey((int)data["userid"]) ? new Player(parser.Players[(int)data["userid"]]) : null;

                //adds the player who took over the bot to currentRoundBotTakeovers
                currentRoundBotTakeovers.Add(botTakeOverArgs.Taker);

                parser.RaiseBotTakeOver(botTakeOverArgs);
            }

            if (eventDescriptor.Name == "begin_new_match") {
                MatchStartedEventArgs matchStartedEventArgs = new MatchStartedEventArgs();

                if (!string.IsNullOrEmpty(parser.Map))
                {
                    matchStartedEventArgs.Mapname = parser.Map.ToString();
                }
                else if (parser.Header != null && !string.IsNullOrEmpty(parser.Header.MapName))
                {
                    matchStartedEventArgs.Mapname = parser.Header.MapName.ToString();
                }
                else
                {
                    matchStartedEventArgs.Mapname = "unknown";
                }

                //makes sure that bombsite triggers' vector values have been set if they exist
                parser.HandleBombSitesAndRescueZones();

                //checks if the map contains bombsite triggers to figure out the gamemode
                var bombsiteCenterA = parser.bombsiteACenter;
                var bombsiteCenterB = parser.bombsiteBCenter;

                bool hasBombsiteA = (bombsiteCenterA.X == 0 && bombsiteCenterA.Y == 0 && bombsiteCenterA.Z == 0 && bombsiteCenterA.Absolute == 0 && bombsiteCenterA.AbsoluteSquared == 0 && bombsiteCenterA.Angle2D == 0) ? false : true;
                bool hasBombsiteB = (bombsiteCenterB.X == 0 && bombsiteCenterB.Y == 0 && bombsiteCenterB.Z == 0 && bombsiteCenterB.Absolute == 0 && bombsiteCenterB.AbsoluteSquared == 0 && bombsiteCenterB.Angle2D == 0) ? false : true;

                matchStartedEventArgs.HasBombsites = (hasBombsiteA || hasBombsiteB) ? true : false;

                parser.RaiseMatchStarted(matchStartedEventArgs);
            }

            if (eventDescriptor.Name == "round_announce_match_start")
                parser.RaiseRoundAnnounceMatchStarted();

            if (eventDescriptor.Name == "round_freeze_end")
            {
                //round length
                timestampFreezetimeEnded = parser.CurrentTime;

                FreezetimeEndedEventArgs freezetimeEnd = new FreezetimeEndedEventArgs()
                {
                    TimeEnd = parser.CurrentTime,
                };

                parser.RaiseFreezetimeEnded(freezetimeEnd);
            }

            //if (eventDescriptor.Name != "player_footstep" && eventDescriptor.Name != "weapon_fire" && eventDescriptor.Name != "player_jump") {
            //	Console.WriteLine (eventDescriptor.Name);
            //}

            switch (eventDescriptor.Name) {
                case "weapon_fire":

                    data = MapData (eventDescriptor, rawEvent);

                    WeaponFiredEventArgs fire = new WeaponFiredEventArgs ();
                    fire.Shooter = parser.Players.ContainsKey ((int)data["userid"]) ? new Player(parser.Players[(int)data["userid"]]) : null;
                    fire.Weapon = new Equipment ((string)data ["weapon"]);

                    if (fire.Shooter != null && fire.Shooter.ActiveWeapon != null && fire.Weapon.Class != EquipmentClass.Grenade) {
                        var originalString = fire.Weapon.OriginalString; // original string is lost when setting hurt.Weapon to hurt.Attacker.ActiveWeapon
                        fire.Weapon = new Player(fire.Shooter).ActiveWeapon;
                        fire.Weapon.Owner = new Player(fire.Weapon.Owner);
                        fire.Weapon.OriginalString = originalString;
                    }

                    fire.TimeInRound = parser.CurrentTime - timestampFreezetimeEnded;

                    parser.RaiseWeaponFired(fire);
                    break;

                /* doesn't seem to trigger this event currently */
                /*
                    case "other_death":
                    data = MapData(eventDescriptor, rawEvent);

                    string entityType = data["othertype"].ToString();

                    if (entityType == "chicken")
                    {
                        ChickenKilledEventArgs chickenKill = new ChickenKilledEventArgs();
                        //long deathLocationX = rawEvent.Keys.

                        parser.RaiseChickenKilled(chickenKill);
                    }

                    parser.RaiseOtherKilled();

                    break;
                */
                case "player_death":
                    data = MapData(eventDescriptor, rawEvent);

                    PlayerKilledEventArgs kill = new PlayerKilledEventArgs();

                    kill.TimeInRound = parser.CurrentTime - timestampFreezetimeEnded;

                    kill.Round = 0;
                    kill.Victim = parser.Players.ContainsKey((int)data["userid"]) ? new Player(parser.Players[(int)data["userid"]]) : null;
                    kill.Killer = parser.Players.ContainsKey((int)data["attacker"]) ? new Player(parser.Players[(int)data["attacker"]]) : null;
                    kill.Assister = parser.Players.ContainsKey((int)data["assister"]) ? new Player(parser.Players[(int)data["assister"]]) : null;
                    kill.Headshot = (bool)data["headshot"];
                    kill.Weapon = new Equipment((string)data["weapon"], (string)data["weapon_itemid"]);

                    // works out if the kill and death were teamkills or suicides
                    kill.TeamKill = false;
                    kill.Suicide = false;

                    if (kill.Victim != null)
                    {
                        if (kill.Victim.SteamID != 0 && kill.Victim.SteamID == kill.Killer?.SteamID)
                        {
                            kill.Suicide = true;
                        }
                        else if (kill.Killer != null && kill.Victim.Team == kill.Killer.Team)
                        {
                            kill.TeamKill = true;
                        }

                        // works out if either the killer or victim have taken over bots
                        kill.KillerBotTakeover = false;
                        kill.VictimBotTakeover = false;
                        kill.AssisterBotTakeover = false;

                        if (kill.Killer != null && currentRoundBotTakeovers.Any(p => p.Name.ToString() == kill.Killer.Name.ToString()))
                        {
                            kill.KillerBotTakeover = true;
                        }
                        if (kill.Victim != null && currentRoundBotTakeovers.Any(p => p.Name.ToString() == kill.Victim.Name.ToString()))
                        {
                            kill.VictimBotTakeover = true;
                        }
                        if (kill.Assister != null && currentRoundBotTakeovers.Any(p => p.Name.ToString() == kill.Assister.Name.ToString()))
                        {
                            kill.AssisterBotTakeover = true;
                        }

                        if (data.ContainsKey("assistedflash"))
                            kill.AssistedFlash = (bool)data["assistedflash"];

                        kill.PenetratedObjects = (int)data["penetrated"];

                        /*if (kill.Killer != null && kill.Weapon.Class != EquipmentClass.Grenade
                            && kill.Weapon.Weapon != EquipmentElement.Revolver
                            && kill.Killer.Weapons.Any() && kill.Weapon.Weapon != EquipmentElement.World) {
                        #if DEBUG
                        if(kill.Weapon.Weapon != kill.Killer.ActiveWeapon.Weapon)
                            throw new InvalidDataException();
                        #endif
                        kill.Weapon = kill.Killer.ActiveWeapon;
                        }*/

                        parser.RaisePlayerKilled(kill);
                    }
                    break;
                case "player_hurt":
                    data = MapData (eventDescriptor, rawEvent);

                    PlayerHurtEventArgs hurt = new PlayerHurtEventArgs ();
                    hurt.Player = parser.Players.ContainsKey((int)data["userid"]) ? new Player(parser.Players[(int)data["userid"]]) : null;
                    hurt.Attacker = parser.Players.ContainsKey((int)data["attacker"]) ? new Player(parser.Players[(int)data ["attacker"]]) : null;
                    hurt.Health = (int)data ["health"];
                    hurt.Armor = (int)data ["armor"];
                    hurt.HealthDamage = (int)data ["dmg_health"];
                    hurt.ArmorDamage = (int)data ["dmg_armor"];
                    hurt.Hitgroup = (Hitgroup)((int)data ["hitgroup"]);

                    hurt.Weapon = new Equipment ((string)data ["weapon"], "");

                    if (hurt.Attacker != null && hurt.Weapon.Class != EquipmentClass.Grenade && hurt.Attacker.Weapons.Any ()) {
                        var originalString = hurt.Weapon.OriginalString; // original string is lost when setting hurt.Weapon to hurt.Attacker.ActiveWeapon
                        hurt.Weapon = new Player(hurt.Attacker).ActiveWeapon;

                        if (hurt.Weapon != null)
                        {
                            hurt.Weapon.Owner = new Player(hurt.Weapon.Owner);
                            hurt.Weapon.OriginalString = originalString;
                            hurt.WeaponString = originalString;
                        }
                    }

                    hurt.TimeInRound = parser.CurrentTime - timestampFreezetimeEnded;

                    hurt.PossiblyKilledByBombExplosion = (hurt.Health == 0 &&
                                                          string.IsNullOrWhiteSpace(hurt.Weapon.OriginalString) &&
                                                          hurt.Weapon.Weapon == EquipmentElement.Unknown &&
                                                          hurt.Weapon.Class == EquipmentClass.Unknown)
                                                              ? true
                                                              : false;

                    parser.RaisePlayerHurt(hurt);
                    break;

                    #region Nades
                case "player_blind":
                    data = MapData(eventDescriptor, rawEvent);

                    if (parser.Players.ContainsKey((int)data["userid"])) {
                        var blindPlayer = parser.Players.ContainsKey((int)data["userid"]) ? new Player(parser.Players[(int)data["userid"]]) : null;

                        if (blindPlayer != null && blindPlayer.Team != Team.Spectate)
                        {
                            BlindEventArgs blind = new BlindEventArgs();
                            blind.Player = blindPlayer;
                            if (data.ContainsKey("attacker") && parser.Players.ContainsKey((int)data["attacker"])) {
                                blind.Attacker = new Player(parser.Players[(int)data["attacker"]]);
                            } else {
                                blind.Attacker = null;
                            }

                            if (data.ContainsKey("blind_duration"))
                                blind.FlashDuration = (float?)data["blind_duration"];
                            else
                                blind.FlashDuration = null;

                            parser.RaiseBlind(blind);
                        }

                        //previous blind implementation
                        blindPlayers.Add(new Player(parser.Players[(int)data["userid"]]));
                    }

                    break;
                case "flashbang_detonate":
                    var args = FillNadeEvent<FlashEventArgs>(MapData(eventDescriptor, rawEvent), parser);
                    args.FlashedPlayers = blindPlayers.ToArray(); //prev blind implementation
                    parser.RaiseFlashExploded(args);
                    blindPlayers.Clear(); //prev blind implementation
                    break;
                case "hegrenade_detonate":
                    parser.RaiseGrenadeExploded(FillNadeEvent<GrenadeEventArgs>(MapData(eventDescriptor, rawEvent), parser));
                    break;
                case "decoy_started":
                    parser.RaiseDecoyStart(FillNadeEvent<DecoyEventArgs>(MapData(eventDescriptor, rawEvent), parser));
                    break;
                case "decoy_detonate":
                    parser.RaiseDecoyEnd(FillNadeEvent<DecoyEventArgs>(MapData(eventDescriptor, rawEvent), parser));
                    break;
                case "smokegrenade_detonate":
                    parser.RaiseSmokeStart(FillNadeEvent<SmokeEventArgs>(MapData(eventDescriptor, rawEvent), parser));
                    break;
                case "smokegrenade_expired":
                    parser.RaiseSmokeEnd(FillNadeEvent<SmokeEventArgs>(MapData(eventDescriptor, rawEvent), parser));
                    break;
                case "inferno_startburn":
                    var fireData = MapData(eventDescriptor, rawEvent);
                    var fireArgs = FillNadeEvent<FireEventArgs>(fireData, parser);
                    var fireStarted = new Tuple<int, FireEventArgs>((int)fireData["entityid"], fireArgs);
                    parser.StartBurnEvents.Enqueue(fireStarted);
                    parser.RaiseFireStart(fireArgs);
                    break;
                case "inferno_expire":
                    var fireEndData = MapData(eventDescriptor, rawEvent);
                    var fireEndArgs = FillNadeEvent<FireEventArgs>(fireEndData, parser);
                    int entityID = (int)fireEndData["entityid"];
                    fireEndArgs.ThrownBy = parser.InfernoOwners[entityID];
                    parser.RaiseFireEnd(fireEndArgs);
                    break;
                    #endregion

                case "player_connect":
                    data = MapData (eventDescriptor, rawEvent);

                    PlayerInfo player = new PlayerInfo ();
                    player.UserID = (int)data ["userid"];
                    player.Name = (string)data ["name"];
                    player.GUID = (string)data ["networkid"];
                    player.XUID = player.GUID == "BOT" ? 0 : GetCommunityID (player.GUID);


                    //player.IsFakePlayer = (bool)data["bot"];

                    int index = (int)data["index"];

                    parser.RawPlayers[index] = player;


                    break;
                case "player_disconnect":
                    data = MapData(eventDescriptor, rawEvent);

                    PlayerDisconnectEventArgs disconnect = new PlayerDisconnectEventArgs();
                    disconnect.Player = parser.Players.ContainsKey((int)data["userid"]) ? new Player(parser.Players[(int)data["userid"]]) : null;
                    parser.RaisePlayerDisconnect(disconnect);

                    int toDelete = (int)data["userid"];
                    for (int i = 0; i < parser.RawPlayers.Length; i++) {

                        if (parser.RawPlayers[i] != null && parser.RawPlayers[i].UserID == toDelete) {
                            parser.RawPlayers[i] = null;
                            break;
                        }
                    }

                    if (parser.Players.ContainsKey(toDelete))
                    {
                        parser.Players.Remove(toDelete);
                    }

                    break;

                case "player_team":
                    data = MapData(eventDescriptor, rawEvent);
                    PlayerTeamEventArgs playerTeamEvent = new PlayerTeamEventArgs();

                    Team t = Team.Spectate;

                    int team = (int)data["team"];

                    if (team == parser.tID)
                        t = Team.Terrorist;
                    else if (team == parser.ctID)
                        t = Team.CounterTerrorist;
                    playerTeamEvent.NewTeam = t;

                    t = Team.Spectate;
                    team = (int)data["oldteam"];
                    if (team == parser.tID)
                        t = Team.Terrorist;
                    else if (team == parser.ctID)
                        t = Team.CounterTerrorist;
                    playerTeamEvent.OldTeam = t;

                    playerTeamEvent.Swapped = parser.Players.ContainsKey((int)data["userid"]) ? new Player(parser.Players[(int)data["userid"]]) : null;
                    playerTeamEvent.IsBot = (bool)data["isbot"];
                    playerTeamEvent.Silent = (bool)data["silent"];

                    parser.RaisePlayerTeam(playerTeamEvent);
                    break;
                case "bomb_beginplant": //When the bomb is starting to get planted
                case "bomb_abortplant": //When the bomb planter stops planting the bomb
                case "bomb_planted": //When the bomb has been planted
                case "bomb_defused": //When the bomb has been defused
                case "bomb_exploded": //When the bomb has exploded
                    data = MapData(eventDescriptor, rawEvent);

                    var bombEventArgs = new BombEventArgs();
                    bombEventArgs.Player = parser.Players.ContainsKey((int)data["userid"]) ? new Player(parser.Players[(int)data["userid"]]) : null;

                    int site = (int)data["site"];

                    //works out which bombsite the bomb was at
                    if (site <= 0)
                    {
                        bombEventArgs.Site = null; // bomb at no bombsite, likely danger zone
                    }
                    else
                    {
                        if (site == parser.bombsiteAIndex)
                        {
                            bombEventArgs.Site = 'A';
                        }
                        else if (site == parser.bombsiteBIndex)
                        {
                            bombEventArgs.Site = 'B';
                        }
                        else
                        {
                            var relevantTrigger = parser.triggers.Single(a => a.Index == site);
                            if (relevantTrigger.Contains(parser.bombsiteACenter))
                            {
                                //planted at A.
                                bombEventArgs.Site = 'A';
                                parser.bombsiteAIndex = site;
                            }
                            else if (relevantTrigger.Contains(parser.bombsiteBCenter))
                            {
                                //planted at B.
                                bombEventArgs.Site = 'B';
                                parser.bombsiteBIndex = site;
                            }
                            else
                            {
                                //where have they planted since 'site' was not 0 ???
                                bombEventArgs.Site = '?';
                            }
                        }
                    }

                    bombEventArgs.TimeInRound = parser.CurrentTime - timestampFreezetimeEnded;


                    switch (eventDescriptor.Name) {
                    case "bomb_beginplant":
                        parser.RaiseBombBeginPlant(bombEventArgs);
                        break;
                    case "bomb_abortplant":
                        parser.RaiseBombAbortPlant(bombEventArgs);
                        break;
                    case "bomb_planted":
                        parser.RaiseBombPlanted(bombEventArgs);
                        break;
                    case "bomb_defused":
                        parser.RaiseBombDefused(bombEventArgs);
                        break;
                    case "bomb_exploded":
                        parser.RaiseBombExploded(bombEventArgs);
                        break;
                    }

                    break;
                case "bomb_begindefuse":
                    data = MapData(eventDescriptor, rawEvent);
                    var e = new BombDefuseEventArgs();
                    e.Player = parser.Players.ContainsKey((int)data["userid"]) ? new Player(parser.Players[(int)data["userid"]]) : null;
                    e.HasKit = (bool)data["haskit"];
                    parser.RaiseBombBeginDefuse(e);
                    break;
                case "bomb_abortdefuse":
                    data = MapData(eventDescriptor, rawEvent);
                    var e2 = new BombDefuseEventArgs();
                    e2.Player = parser.Players.ContainsKey((int)data["userid"]) ? new Player(parser.Players[(int)data["userid"]]) : null;
                    e2.HasKit = e2.Player.HasDefuseKit;
                    parser.RaiseBombAbortDefuse(e2);
                    break;

                case "hostage_rescued":
                    data = MapData(eventDescriptor, rawEvent);
                    var rescued = new HostageRescuedEventArgs();
                    rescued.Round = 0; // worked out in DemoProcessor
                    rescued.Player = parser.Players.ContainsKey((int)data["userid"]) ? new Player(parser.Players[(int)data["userid"]]) : null;

                    //currently assumes only one rescue zone,
                    //"site" may indicate the hostage rescue zone number (eg. 0, 1, ...) or it may just always be set to 0. //// Attempted to test this in danger zone but the event does not seem to be thrown when a hostage is taken to a rescue zone
                    int rescueZone = (int)data["site"];
                    parser.rescueZoneIndex = rescueZone;
                    rescued.RescueZone = rescueZone;

                    int hostage = (int)data["hostage"];

                    //works out which hostage was rescued
                    if (hostage == parser.hostageAIndex)
                    {
                        rescued.Hostage = 'A';
                        rescued.HostageIndex = parser.hostageAIndex;
                    }
                    else if (hostage == parser.hostageBIndex)
                    {
                        rescued.Hostage = 'B';
                        rescued.HostageIndex = parser.hostageBIndex;
                    }
                    else
                    {
                        if (parser.hostageAIndex == -1)
                        {
                            rescued.Hostage = 'A';
                            parser.hostageAIndex = hostage;
                            rescued.HostageIndex = parser.hostageAIndex;
                        }
                        else if (parser.hostageBIndex == -1)
                        {
                            rescued.Hostage = 'B';
                            parser.hostageBIndex = hostage;
                            rescued.HostageIndex = parser.hostageBIndex;
                        }
                        else
                        {
                            // a third hostage???
                            rescued.Hostage = '?';
                        }
                    }

                    rescued.TimeInRound = parser.CurrentTime - timestampFreezetimeEnded;

                    parser.RaiseHostageRescued(rescued);
                    break;

                case "hostage_follows":
                    data = MapData(eventDescriptor, rawEvent);
                    var pickedUp = new HostagePickedUpEventArgs();
                    pickedUp.Round = 0; // worked out in DemoProcessor
                    pickedUp.Player = parser.Players.ContainsKey((int)data["userid"]) ? new Player(parser.Players[(int)data["userid"]]) : null;

                    hostage = (int)data["hostage"];

                    //works out which hostage was picked up
                    if (hostage == parser.hostageAIndex)
                    {
                        pickedUp.Hostage = 'A';
                        pickedUp.HostageIndex = parser.hostageAIndex;
                    }
                    else if (hostage == parser.hostageBIndex)
                    {
                        pickedUp.Hostage = 'B';
                        pickedUp.HostageIndex = parser.hostageBIndex;
                    }
                    else
                    {
                        if (parser.hostageAIndex == -1)
                        {
                            pickedUp.Hostage = 'A';
                            parser.hostageAIndex = hostage;
                            pickedUp.HostageIndex = parser.hostageAIndex;
                        }
                        else if (parser.hostageBIndex == -1)
                        {
                            pickedUp.Hostage = 'B';
                            parser.hostageBIndex = hostage;
                            pickedUp.HostageIndex = parser.hostageBIndex;
                        }
                        else
                        {
                            // a third hostage???
                            pickedUp.Hostage = '?';
                        }
                    }

                    pickedUp.TimeInRound = parser.CurrentTime - timestampFreezetimeEnded;

                    parser.RaiseHostagePickedUp(pickedUp);
                    break;

            }
        }

        private static T FillNadeEvent<T>(Dictionary<string, object> data, DemoParser parser) where T : NadeEventArgs, new()
        {
            var nade = new T();

            if (data.ContainsKey("userid") && parser.Players.ContainsKey((int)data["userid"]))
                nade.ThrownBy = new Player(parser.Players[(int)data["userid"]]);

            Vector vec = new Vector();
            vec.X = (float)data["x"];
            vec.Y = (float)data["y"];
            vec.Z = (float)data["z"];
            nade.Position = vec;

            return nade;
        }

        private static Dictionary<string, object> MapData(GameEventList.Descriptor eventDescriptor, GameEvent rawEvent)
        {
            Dictionary<string, object> data = new Dictionary<string, object>();

            for (int i = 0; i < eventDescriptor.Keys.Length; i++)
                data.Add(eventDescriptor.Keys[i].Name, rawEvent.Keys[i]);

            return data;
        }

        private static long GetCommunityID(string steamID)
        {
            long authServer = Convert.ToInt64(steamID.Substring(8, 1));
            long authID = Convert.ToInt64(steamID.Substring(10));
            return (76561197960265728 + (authID * 2) + authServer);
        }
    }
}
