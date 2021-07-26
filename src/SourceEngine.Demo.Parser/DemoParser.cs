using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

#if SAVE_PROP_VALUES
using System.Text;
#endif

using SourceEngine.Demo.Parser.BitStream;
using SourceEngine.Demo.Parser.DataTable;
using SourceEngine.Demo.Parser.Entities;
using SourceEngine.Demo.Parser.Messages.Fast.Net;
using SourceEngine.Demo.Parser.Packet;
using SourceEngine.Demo.Parser.StringTable;
using SourceEngine.Demo.Parser.Structs;

namespace SourceEngine.Demo.Parser
{
    #if CHECK_INTEGRITY
    #warning The DemoParser is very slow when compiled with integrity checks enabled. Compile in the Release configuration for performance.
    #endif
    #if SAVE_PROP_VALUES
    #warning Compiling with the SavePropValues configuration. It's intended only for debugging - taking an (entity) dump; don't use it in production.
    #endif
    public class DemoParser : IDisposable
    {
        private const int MAX_EDICT_BITS = 11;
        internal const int INDEX_MASK = (1 << MAX_EDICT_BITS) - 1;
        internal const int MAX_ENTITIES = 1 << MAX_EDICT_BITS;
        private const int MAXPLAYERS = 64;
        private const int MAXWEAPONS = 64;

        public bool stopParsingDemo = false;
        public readonly bool ParseChickens;
        public readonly bool ParsePlayerPositions;

        // this MAY work up to 4 (since it uses 000, 001, 002 & 003)
        private readonly uint numOfHostageRescueZonesLookingFor;

        public readonly Dictionary<int, BoundingBox> Triggers = new();
        internal readonly Dictionary<int, Player> InfernoOwners = new();

        #region Events

        /// <summary>
        /// Raised once when finished parsing the demo
        /// Shows player locations every second
        /// </summary>
        public event EventHandler<PlayerPositionsEventArgs> PlayerPositions;

        /// <summary>
        /// Raised once when the Header of the demo is parsed
        /// </summary>
        public event EventHandler<HeaderParsedEventArgs> HeaderParsed;

        /// <summary>
        /// Occurs when the match started, so when the "begin_new_match"-GameEvent is dropped.
        /// This usually right before the freezetime of the 1st round. Be careful, since the players
        /// usually still have warmup-money when this drops.
        /// </summary>
        public event EventHandler<MatchStartedEventArgs> MatchStarted;

        /// <summary>
        /// Occurs when the first round of a new match start "round_announce_match_start"
        /// </summary>
        public event EventHandler<RoundAnnounceMatchStartedEventArgs> RoundAnnounceMatchStarted;

        /// <summary>
        /// Occurs when round starts, on the round_start event of the demo. Usually the players haven't spawned yet, but have
        /// received the money for the next round.
        /// </summary>
        public event EventHandler<RoundStartedEventArgs> RoundStart;

        /// <summary>
        /// Occurs when round ends
        /// </summary>
        public event EventHandler<RoundEndedEventArgs> RoundEnd;

        /// <summary>
        /// Occurs when round ends
        /// </summary>
        public event EventHandler<SwitchSidesEventArgs> SwitchSides;

        /// <summary>
        /// Occurs at the end of the match, when the scoreboard is shown
        /// </summary>
        public event EventHandler<WinPanelMatchEventArgs> WinPanelMatch;

        /// <summary>
        /// Occurs when it's the last round of a match
        /// </summary>
        public event EventHandler<RoundFinalEventArgs> RoundFinal;

        /// <summary>
        /// Occurs at the half of a side
        /// </summary>
        public event EventHandler<LastRoundHalfEventArgs> LastRoundHalf;

        /// <summary>
        /// Occurs when round really ended
        /// </summary>
        public event EventHandler<RoundOfficiallyEndedEventArgs> RoundOfficiallyEnded;

        /// <summary>
        /// Occurs on round end with the MVP
        /// </summary>
        public event EventHandler<RoundMVPEventArgs> RoundMVP;

        /// <summary>
        /// Occurs when a player take control of a bot
        /// </summary>
        public event EventHandler<BotTakeOverEventArgs> BotTakeOver;

        /// <summary>
        /// Occurs when freezetime ended. Raised on "round_freeze_end"
        /// </summary>
        public event EventHandler<FreezetimeEndedEventArgs> FreezetimeEnded;

        /// <summary>
        /// Occurs on the end of every tick, after the game events were processed and the packet-entities updated
        /// </summary>
        public event EventHandler<TickDoneEventArgs> TickDone;

        /// <summary>
        /// This is raised when an entity is killed.
        /// </summary>
        public event EventHandler<OtherKilledEventArgs> OtherKilled;

        /// <summary>
        /// This is raised when an entity that is killed is a chicken.
        /// </summary>
        public event EventHandler<ChickenKilledEventArgs> ChickenKilled;

        /// <summary>
        /// This is raised when a player is killed. Not that the killer might be dead by the time is raised (e.g. nade-kills),
        /// also note that the killed player is still alive when this is killed
        /// </summary>
        public event EventHandler<PlayerKilledEventArgs> PlayerKilled;

        /// <summary>
        /// Occurs when a player select a team
        /// </summary>
        public event EventHandler<PlayerTeamEventArgs> PlayerTeam;

        /// <summary>
        /// Occurs when a weapon is fired.
        /// </summary>
        public event EventHandler<WeaponFiredEventArgs> WeaponFired;

        /// <summary>
        /// Occurs when smoke nade started.
        /// </summary>
        public event EventHandler<SmokeEventArgs> SmokeNadeStarted;

        /// <summary>
        /// Occurs when smoke nade ended.
        /// Hint: When a round ends, this is *not* called.
        /// Make sure to clear nades yourself at the end of rounds
        /// </summary>
        public event EventHandler<SmokeEventArgs> SmokeNadeEnded;

        /// <summary>
        /// Occurs when decoy nade started.
        /// </summary>
        public event EventHandler<DecoyEventArgs> DecoyNadeStarted;

        /// <summary>
        /// Occurs when decoy nade ended.
        /// Hint: When a round ends, this is *not* called.
        /// Make sure to clear nades yourself at the end of rounds
        /// </summary>
        public event EventHandler<DecoyEventArgs> DecoyNadeEnded;

        /// <summary>
        /// Occurs when a fire nade (incendiary / molotov) started.
        /// This currently *doesn't* contain who it threw since this is for some weird reason not networked
        /// </summary>
        public event EventHandler<FireEventArgs> FireNadeStarted;

        /// <summary>
        /// FireNadeStarted, but with correct ThrownBy player.
        /// Hint: Raised at the end of inferno_startburn tick instead of exactly when the event is parsed
        /// </summary>
        public event EventHandler<FireEventArgs> FireNadeWithOwnerStarted;

        /// <summary>
        /// Occurs when fire nade ended.
        /// Hint: When a round ends, this is *not* called.
        /// Make sure to clear nades yourself at the end of rounds
        /// </summary>
        public event EventHandler<FireEventArgs> FireNadeEnded;

        /// <summary>
        /// Occurs when flash nade exploded.
        /// </summary>
        public event EventHandler<FlashEventArgs> FlashNadeExploded;

        /// <summary>
        /// Occurs when explosive nade exploded.
        /// </summary>
        public event EventHandler<GrenadeEventArgs> ExplosiveNadeExploded;

        /// <summary>
        /// Occurs when any nade reached it's target.
        /// </summary>
        public event EventHandler<NadeEventArgs> NadeReachedTarget;

        /// <summary>
        /// Occurs when bomb is being planted.
        /// </summary>
        public event EventHandler<BombEventArgs> BombBeginPlant;

        /// <summary>
        /// Occurs when the plant is aborted
        /// </summary>
        public event EventHandler<BombEventArgs> BombAbortPlant;

        /// <summary>
        /// Occurs when the bomb has been planted.
        /// </summary>
        public event EventHandler<BombEventArgs> BombPlanted;

        /// <summary>
        /// Occurs when the bomb has been defused.
        /// </summary>
        public event EventHandler<BombEventArgs> BombDefused;

        /// <summary>
        /// Occurs when bomb has exploded.
        /// </summary>
        public event EventHandler<BombEventArgs> BombExploded;

        /// <summary>
        /// Occurs when someone begins to defuse the bomb.
        /// </summary>
        public event EventHandler<BombDefuseEventArgs> BombBeginDefuse;

        /// <summary>
        /// Occurs when someone aborts to defuse the bomb.
        /// </summary>
        public event EventHandler<BombDefuseEventArgs> BombAbortDefuse;

        /// <summary>
        /// Occurs when someone rescues a hostage.
        /// </summary>
        public event EventHandler<HostageRescuedEventArgs> HostageRescued;

        /// <summary>
        /// Occurs when someone picks up a hostage.
        /// </summary>
        public event EventHandler<HostagePickedUpEventArgs> HostagePickedUp;

        /// <summary>
        /// Occurs when an player is attacked by another player.
        /// Hint: Only occurs in GOTV-demos.
        /// </summary>
        public event EventHandler<PlayerHurtEventArgs> PlayerHurt;

        /// <summary>
        /// Occurs when player is blinded by flashbang
        /// Hint: The order of the blind event and FlashNadeExploded event is not always the same
        /// </summary>
        public event EventHandler<BlindEventArgs> Blind;

        /// <summary>
        /// Occurs when the player object is first updated to reference all the necessary information
        /// Hint: Event will be raised when any player with a SteamID connects, not just PlayingParticipants
        /// </summary>
        public event EventHandler<PlayerBindEventArgs> PlayerBind;

        /// <summary>
        /// Occurs when a player disconnects from the server.
        /// </summary>
        public event EventHandler<PlayerDisconnectEventArgs> PlayerDisconnect;

        /// <summary>
        /// Occurs when the server uses the "say" command
        /// </summary>
        public event EventHandler<SayTextEventArgs> SayText;

        /// <summary>
        /// Occurs when a player uses the "say" command
        /// </summary>
        public event EventHandler<SayText2EventArgs> SayText2;

        /// <summary>
        /// Occurs when the server display a player rank
        /// </summary>
        public event EventHandler<RankUpdateEventArgs> RankUpdate;

        public event EventHandler<ServerInfoEventArgs> ServerInfo;

        #endregion

        /// <summary>
        /// The map name of the Demo. Only available after the header is parsed.
        /// Is a string like "de_dust2".
        /// </summary>
        /// <value>The map.</value>
        public string Map => Header?.MapName;

        /// <summary>
        /// The header of the demo, containing some useful information.
        /// </summary>
        /// <value>The header.</value>
        public DemoHeader Header { get; private set; }

        /// <summary>
        /// Gets the participants of this game
        /// </summary>
        /// <value>The participants.</value>
        public IEnumerable<Player> Participants => Players.Values;

        /// <summary>
        /// Gets all the participants of this game, that aren't spectating.
        /// </summary>
        /// <value>The playing participants.</value>
        public IEnumerable<Player> PlayingParticipants
        {
            get { return Players.Values.Where(a => a.Team != Team.Spectate); }
        }

        /// <summary>
        /// The stream of the demo - all the information go here
        /// </summary>
        private readonly IBitStream BitStream;

        /// <summary>
        /// A parser for DataTables. This contains the ServerClasses and DataTables.
        /// </summary>
        internal readonly DataTableParser SendTableParser = new();

        /// <summary>
        /// This maps an ServerClass to an Equipment.
        /// Note that this is wrong for the CZ,M4A1 and USP-S, there is an additional fix for those
        /// </summary>
        internal readonly Dictionary<ServerClass, EquipmentElement> equipmentMapping = new();

        internal readonly Dictionary<int, Player> Players = new();

        /// <summary>
        /// Containing info about players, accessible by the entity-id
        /// </summary>
        internal readonly Player[] PlayerInformations = new Player[MAXPLAYERS];

        /// <summary>
        /// Contains information about the players, accessible by the userid.
        /// </summary>
        internal readonly PlayerInfo[] RawPlayers = new PlayerInfo[MAXPLAYERS];

        /// <summary>
        /// All entities currently alive in the demo.
        /// </summary>
        internal readonly Entity[] Entities = new Entity[MAX_ENTITIES]; //Max 2048 entities.

        /// <summary>
        /// The model precache. With this we can tell which model an entity has.
        /// Useful for finding out whether a weapon is a P250 or a CZ
        /// </summary>
        internal readonly List<string> modelprecache = new();

        /// <summary>
        /// The string tables sent by the server.
        /// </summary>
        internal readonly List<CreateStringTable> stringTables = new();

        /// <summary>
        /// Map an entity to a weapon. Used to remember whether a weapon is a p250,
        /// how much ammunition it has, etc.
        /// </summary>
        private readonly Equipment[] weapons = new Equipment[MAX_ENTITIES];

        /// <summary>
        /// The indices of the bombsites - useful to find out
        /// where the bomb is planted
        /// </summary>
        public int bombsiteAIndex { get; internal set; } = -1;

        public int bombsiteBIndex { get; internal set; } = -1;

        public Vector bombsiteACenter { get; internal set; }

        public Vector bombsiteBCenter { get; internal set; }

        /// <summary>
        /// The indices of the hostages - useful to find out
        /// which hostage has been rescued
        /// </summary>
        public int hostageAIndex { get; internal set; } = -1;

        public int hostageBIndex { get; internal set; } = -1;

        public int rescueZoneIndex { get; internal set; } = -1;

        public Dictionary<int, Vector> rescueZoneCenters { get; internal set; } = new();

        /// <summary>
        /// The ID of the CT-Team
        /// </summary>
        internal int ctID = -1;

        /// <summary>
        /// The ID of the terrorist team
        /// </summary>
        internal int tID = -1;

        /// <summary>
        /// The Rounds the Counter-Terrorists have won at this point.
        /// </summary>
        /// <value>The CT score.</value>
        public int CTScore { get; private set; }

        /// <summary>
        /// The Rounds the Terrorists have won at this point.
        /// </summary>
        /// <value>The T score.</value>
        public int TScore { get; private set; }

        /// <summary>
        /// The clan name of the Counter-Terrorists
        /// </summary>
        /// <value>The name of the CT clan.</value>
        public string CTClanName { get; private set; }

        /// <summary>
        /// The clan name of the Terrorists
        /// </summary>
        /// <value>The name of the T clan.</value>
        public string TClanName { get; private set; }

        /// <summary>
        /// The flag of the Counter-Terrorists
        /// </summary>
        /// <value>The flag of the CT clan.</value>
        public string CTFlag { get; private set; }

        /// <summary>
        /// The flag of the Terrorists
        /// </summary>
        /// <value>The flag of the T clan.</value>
        public string TFlag { get; private set; }

        /// <summary>
        /// And GameEvent is just sent with ID |--> Value, but we need Name |--> Value.
        /// Luckily these contain a map ID |--> Name.
        /// </summary>
        internal Dictionary<int, GameEventList.Descriptor> EventDescriptors = null;

        /// <summary>
        /// The blind players, so we can tell who was flashed by a flashbang.
        /// previous blind implementation
        /// </summary>
        internal readonly List<Player> BlindPlayers = new();

        /// <summary>
        /// Holds inferno_startburn event args so they can be matched with player
        /// </summary>
        internal readonly Queue<Tuple<int, FireEventArgs>> StartBurnEvents = new();

        // These could be Dictionary<int, RecordedPropertyUpdate[]>, but I was too lazy to
        // define that class. Also: It doesn't matter anyways, we always have to cast.

        /// <summary>
        /// The preprocessed baselines, useful to create entities fast
        /// </summary>
        internal readonly Dictionary<int, object[]> PreprocessedBaselines = new();

        /// <summary>
        /// The instance baselines.
        /// When a new edict is created one would need to send all the information twice.
        /// Since this is (was) expensive, valve sends an instancebaseline, which contains defaults
        /// for all the properties.
        /// </summary>
        internal readonly Dictionary<int, byte[]> instanceBaseline = new();

        /// <summary>
        /// The tick rate *of the demo* (16 for normal GOTV-demos)
        /// </summary>
        /// <value>The tick rate.</value>
        public float TickRate => Header.PlaybackFrames / Header.PlaybackTime;

        /// <summary>
        /// How long a tick of the demo is in s^-1
        /// </summary>
        /// <value>The tick time.</value>
        public float TickTime => Header.PlaybackTime / Header.PlaybackFrames;

        /// <summary>
        /// Gets the parsing progress. 0 = beginning, ~1 = finished (it can actually be > 1, so be careful!)
        /// </summary>
        /// <value>The parsing progress.</value>
        public float ParsingProgess => CurrentTick / (float)Header.PlaybackFrames;

        /// <summary>
        /// The current tick the parser has seen. So if it's a 16-tick demo,
        /// it will have 16 after one second.
        /// </summary>
        /// <value>The current tick.</value>
        public int CurrentTick { get; private set; }

        /// <summary>
        /// The current in-game-tick as reported by the demo-file.
        /// </summary>
        /// <value>The current tick.</value>
        public int IngameTick { get; internal set; }

        /// <summary>
        /// How far we've advanced in the demo in seconds.
        /// </summary>
        /// <value>The current time.</value>
        public float CurrentTime => CurrentTick * TickTime;

        /// <summary>
        /// This contains additional information about each player, such as Kills, Deaths, etc.
        /// This is networked separately from the player, so we need to cache it somewhere else.
        /// </summary>
        private readonly PlayerResource[] additionalInformations = new PlayerResource[MAXPLAYERS];

        /// <summary>
        /// Initializes a new DemoParser. Right point if you want to start analyzing demos.
        /// Hint: ParseHeader() is probably what you want to look into next.
        /// </summary>
        /// <param name="input">An input-stream.</param>
        /// <param name="parseChickens"><c>true</c> if chickens should be counted; <c>false</c> otherwise.</param>
        /// <param name="parsePlayerPositions">
        /// <c>true</c> if player positions should be parsed; <c>false</c> otherwise.
        /// </param>
        /// <param name="hostagerescuezonecountoverride">Amount of hostage rescue zones to assume are present.</param>
        public DemoParser(
            Stream input,
            bool parseChickens = true,
            bool parsePlayerPositions = true,
            uint hostagerescuezonecountoverride = 0)
        {
            BitStream = BitStreamUtil.Create(input);

            for (int i = 0; i < MAXPLAYERS; i++)
                additionalInformations[i] = new PlayerResource();

            this.ParseChickens = parseChickens;
            this.ParsePlayerPositions = parsePlayerPositions;
            numOfHostageRescueZonesLookingFor = hostagerescuezonecountoverride;
        }

        /// <summary>
        /// Parses the header (first few hundred bytes) of the demo.
        /// </summary>
        public void ParseHeader()
        {
            DemoHeader header = DemoHeader.ParseFrom(BitStream);

            if (header.Filestamp != "HL2DEMO")
                throw new InvalidDataException("Invalid File-Type - expecting HL2DEMO");

            if (header.GameDirectory != "csgo")
                throw new InvalidDataException("Invalid Demo-Game");

            if (header.Protocol != 4)
                throw new InvalidDataException("Invalid Demo-Protocol");

            Header = header;

            HeaderParsed?.Invoke(this, new HeaderParsedEventArgs(Header));
        }

        /// <summary>
        /// Parses this file until the end of the demo is reached.
        /// Useful if you have subscribed to events
        /// </summary>
        public void ParseToEnd()
        {
            ParseToEnd(CancellationToken.None);
        }

        /// <summary>
        /// Same as ParseToEnd() but accepts a CancellationToken to be able to cancel parsing
        /// </summary>
        /// <param name="token"></param>
        public void ParseToEnd(CancellationToken token)
        {
            if (ParsePlayerPositions)
            {
                int secondsPassed = 0;

                while (ParseNextTick())
                {
                    if (token.IsCancellationRequested || stopParsingDemo) return;

                    if ((int)CurrentTime > secondsPassed)
                    {
                        RecordPlayerPositions();
                        secondsPassed = (int)CurrentTime;
                    }
                }
            }
            else
            {
                while (ParseNextTick())
                {
                    if (token.IsCancellationRequested || stopParsingDemo)
                        return;
                }
            }
        }

        private void RecordPlayerPositions()
        {
            var playerPositionsEventArgs = new PlayerPositionsEventArgs
            {
                CurrentTime = CurrentTime,
                PlayerPositions = new List<PlayerPositionEventArgs>(),
            };

            foreach (Player participant in PlayingParticipants)
            {
                var player = new Player(participant);

                playerPositionsEventArgs.PlayerPositions.Add(
                    new PlayerPositionEventArgs
                    {
                        Player = player,
                    }
                );
            }

            RaisePlayerPositions(playerPositionsEventArgs);
        }

        /// <summary>
        /// Parses the next tick of the demo.
        /// </summary>
        /// <returns><c>true</c>, if this wasn't the last tick, <c>false</c> otherwise.</returns>
        public bool ParseNextTick()
        {
            if (Header == null)
                throw new InvalidOperationException(
                    "You need to call ParseHeader first before you call ParseToEnd or ParseNextTick!"
                );

            bool b = ParseTick();

            for (int i = 0; i < RawPlayers.Length; i++)
            {
                if (RawPlayers[i] == null)
                    continue;

                PlayerInfo rawPlayer = RawPlayers[i];

                if (rawPlayer.GUID.Equals("BOT") && !rawPlayer.Name.Equals("GOTV"))
                {
                    rawPlayer.Name = PlayerInformations[i] != null
                        ? PlayerInformations[i].Name != null ? PlayerInformations[i].Name : rawPlayer.Name
                        : rawPlayer.Name;

                    rawPlayer.GUID = "Unknown";
                }

                int id = rawPlayer.UserID;

                if (PlayerInformations[i] != null)
                {
                    //There is an good entity for this
                    bool newplayer = false;

                    if (!Players.ContainsKey(id))
                    {
                        Players[id] = PlayerInformations[i];
                        newplayer = true;
                    }

                    Player p = Players[id];
                    p.Name = rawPlayer.Name;
                    p.SteamID = rawPlayer.XUID;

                    p.UserID = id;

                    p.PlayerResource = additionalInformations[p.EntityID];

                    if (p.IsAlive)
                        p.LastAlivePosition = p.Position.Copy();

                    if (newplayer && p.SteamID != 0)
                    {
                        var bind = new PlayerBindEventArgs { Player = p };
                        RaisePlayerBind(bind);
                    }
                }
            }

            while (StartBurnEvents.Count > 0)
            {
                (int entityId, FireEventArgs eventArgs) = StartBurnEvents.Dequeue();
                eventArgs.ThrownBy = InfernoOwners[entityId];
                RaiseFireWithOwnerStart(eventArgs);
            }

            if (b)
                TickDone?.Invoke(this, new TickDoneEventArgs());

            return b;
        }

        /// <summary>
        /// Parses the tick internally
        /// </summary>
        /// <returns><c>true</c>, if tick was parsed, <c>false</c> otherwise.</returns>
        private bool ParseTick()
        {
            var command = (DemoCommand)BitStream.ReadByte();

            IngameTick = (int)BitStream.ReadInt(32); // tick number
            BitStream.ReadByte(); // player slot

            CurrentTick++; // = TickNum;

            switch (command)
            {
                case DemoCommand.SyncTick:
                    break;
                case DemoCommand.Stop:
                    return false;
                case DemoCommand.ConsoleCommand:
                    BitStream.BeginChunk(BitStream.ReadSignedInt(32) * 8);
                    BitStream.EndChunk();
                    break;
                case DemoCommand.DataTables:
                    BitStream.BeginChunk(BitStream.ReadSignedInt(32) * 8);
                    SendTableParser.ParsePacket(BitStream);
                    BitStream.EndChunk();

                    //Map the weapons in the equipmentMapping-Dictionary.
                    MapEquipment();

                    //And now we have the entities, we can bind events on them.
                    BindEntites();

                    break;
                case DemoCommand.StringTables:
                    BitStream.BeginChunk(BitStream.ReadSignedInt(32) * 8);
                    StringTableParser.ParsePacket(BitStream, this);
                    BitStream.EndChunk();
                    break;
                case DemoCommand.UserCommand:
                    BitStream.ReadInt(32);
                    BitStream.BeginChunk(BitStream.ReadSignedInt(32) * 8);
                    BitStream.EndChunk();
                    break;
                case DemoCommand.Signon:
                case DemoCommand.Packet:
                    ParseDemoPacket();
                    break;
                default:
                    throw new Exception("Can't handle Demo-Command " + command);
            }

            return true;
        }

        /// <summary>
        /// Parses a DEM_Packet.
        /// </summary>
        private void ParseDemoPacket()
        {
            //Read a command-info. Contains no really useful information afaik.
            CommandInfo.Parse(BitStream);
            BitStream.ReadInt(32); // SeqNrIn
            BitStream.ReadInt(32); // SeqNrOut

            BitStream.BeginChunk(BitStream.ReadSignedInt(32) * 8);
            DemoPacketParser.ParsePacket(BitStream, this, ParseChickens);
            BitStream.EndChunk();
        }

        /// <summary>
        /// Binds the events for entities. And Entity has many properties.
        /// You can subscribe to when an entity of a specific class is created,
        /// and then you can subscribe to updates of properties of this entity.
        /// This is a bit complex, but very fast.
        /// </summary>
        private void BindEntites()
        {
            //Okay, first the team-stuff.
            HandleTeamScores();

            HandleBombSitesAndRescueZones();

            HandlePlayers();

            HandleWeapons();

            HandleInfernos();
        }

        private void HandleTeamScores()
        {
            SendTableParser.FindByName("CCSTeam").OnNewEntity += (_, e) =>
            {
                string team = null;
                string teamName = null;
                string teamFlag;
                int teamID = -1;
                int score = 0;

                e.Entity.FindProperty("m_scoreTotal").IntRecived += (_, update) => { score = update.Value; };

                e.Entity.FindProperty("m_iTeamNum").IntRecived += (_, update) =>
                {
                    teamID = update.Value;

                    if (team == "CT")
                    {
                        ctID = teamID;
                        CTScore = score;
                        foreach (Player p in PlayerInformations.Where(a => a != null && a.TeamID == teamID))
                            p.Team = Team.CounterTerrorist;
                    }

                    if (team == "TERRORIST")
                    {
                        tID = teamID;
                        TScore = score;
                        foreach (Player p in PlayerInformations.Where(a => a != null && a.TeamID == teamID))
                            p.Team = Team.Terrorist;
                    }
                };

                e.Entity.FindProperty("m_szTeamname").StringRecived += (_, recivedTeamName) =>
                {
                    team = recivedTeamName.Value;

                    //We got the name. Lets bind the updates accordingly!
                    if (recivedTeamName.Value == "CT")
                    {
                        CTScore = score;
                        CTClanName = teamName;
                        e.Entity.FindProperty("m_scoreTotal").IntRecived += (_, update) => { CTScore = update.Value; };

                        if (teamID != -1)
                        {
                            ctID = teamID;
                            foreach (Player p in PlayerInformations.Where(a => a != null && a.TeamID == teamID))
                                p.Team = Team.CounterTerrorist;
                        }
                    }
                    else if (recivedTeamName.Value == "TERRORIST")
                    {
                        TScore = score;
                        TClanName = teamName;
                        e.Entity.FindProperty("m_scoreTotal").IntRecived += (_, update) => { TScore = update.Value; };

                        if (teamID != -1)
                        {
                            tID = teamID;
                            foreach (Player p in PlayerInformations.Where(a => a != null && a.TeamID == teamID))
                                p.Team = Team.Terrorist;
                        }
                    }
                };

                e.Entity.FindProperty("m_szTeamFlagImage").StringRecived += (_, recivedTeamFlag) =>
                {
                    teamFlag = recivedTeamFlag.Value;

                    if (team == "CT")
                        CTFlag = teamFlag;
                    else if (team == "TERRORIST")
                        TFlag = teamFlag;
                };

                e.Entity.FindProperty("m_szClanTeamname").StringRecived += (_, recivedClanName) =>
                {
                    teamName = recivedClanName.Value;

                    if (team == "CT")
                        CTClanName = recivedClanName.Value;
                    else if (team == "TERRORIST")
                        TClanName = recivedClanName.Value;
                };
            };
        }

        private void HandlePlayers()
        {
            SendTableParser.FindByName("CCSPlayer").OnNewEntity += (_, e) => HandleNewPlayer(e.Entity);

            SendTableParser.FindByName("CCSPlayerResource").OnNewEntity += (_, playerResources) =>
            {
                for (int i = 0; i < 64; i++)
                {
                    //Since this is passed as reference to the delegates
                    int iForTheMethod = i;
                    string iString = i.ToString().PadLeft(3, '0');

                    playerResources.Entity.FindProperty("m_szClan." + iString).StringRecived += (_, e) =>
                    {
                        additionalInformations[iForTheMethod].Clantag = e.Value;
                    };

                    playerResources.Entity.FindProperty("m_iPing." + iString).IntRecived += (_, e) =>
                    {
                        additionalInformations[iForTheMethod].Ping = e.Value;
                    };

                    playerResources.Entity.FindProperty("m_iScore." + iString).IntRecived += (_, e) =>
                    {
                        additionalInformations[iForTheMethod].Score = e.Value;
                    };

                    playerResources.Entity.FindProperty("m_iKills." + iString).IntRecived += (_, e) =>
                    {
                        additionalInformations[iForTheMethod].Kills = e.Value;
                    };

                    playerResources.Entity.FindProperty("m_iDeaths." + iString).IntRecived += (_, e) =>
                    {
                        additionalInformations[iForTheMethod].Deaths = e.Value;
                    };

                    playerResources.Entity.FindProperty("m_iAssists." + iString).IntRecived += (_, e) =>
                    {
                        additionalInformations[iForTheMethod].Assists = e.Value;
                    };

                    playerResources.Entity.FindProperty("m_iMVPs." + iString).IntRecived += (_, e) =>
                    {
                        additionalInformations[iForTheMethod].MVPs = e.Value;
                    };

                    playerResources.Entity.FindProperty("m_iTotalCashSpent." + iString).IntRecived += (_, e) =>
                    {
                        additionalInformations[iForTheMethod].TotalCashSpent = e.Value;
                    };

                    #if DEBUG
                    playerResources.Entity.FindProperty("m_iArmor." + iString).IntRecived += (_, e) =>
                    {
                        additionalInformations[iForTheMethod].ScoreboardArmor = e.Value;
                    };

                    playerResources.Entity.FindProperty("m_iHealth." + iString).IntRecived += (_, e) =>
                    {
                        additionalInformations[iForTheMethod].ScoreboardHP = e.Value;
                    };

                    #endif
                }
            };
        }

        private void HandleNewPlayer(Entity playerEntity)
        {
            Player p;

            if (PlayerInformations[playerEntity.ID - 1] != null)
            {
                p = PlayerInformations[playerEntity.ID - 1];
            }
            else
            {
                p = new Player();
                PlayerInformations[playerEntity.ID - 1] = p;
                p.SteamID = -1;
                p.Name = "unconnected";
            }

            p.EntityID = playerEntity.ID;
            p.Entity = playerEntity;
            p.Position = new Vector();
            p.Velocity = new Vector();

            //position update
            playerEntity.FindProperty("cslocaldata.m_vecOrigin").VectorRecived += (_, e) =>
            {
                p.Position.X = e.Value.X;
                p.Position.Y = e.Value.Y;
            };

            playerEntity.FindProperty("cslocaldata.m_vecOrigin[2]").FloatRecived +=
                (_, e) => { p.Position.Z = e.Value; };

            //team update
            //problem: Teams are networked after the players... How do we solve that?
            playerEntity.FindProperty("m_iTeamNum").IntRecived += (_, e) =>
            {
                p.TeamID = e.Value;

                if (e.Value == ctID)
                    p.Team = Team.CounterTerrorist;
                else if (e.Value == tID)
                    p.Team = Team.Terrorist;
                else
                    p.Team = Team.Spectate;
            };

            //update some stats
            playerEntity.FindProperty("m_iHealth").IntRecived += (_, e) => p.HP = e.Value;
            playerEntity.FindProperty("m_ArmorValue").IntRecived += (_, e) => p.Armor = e.Value;
            playerEntity.FindProperty("m_bHasDefuser").IntRecived += (_, e) => p.HasDefuseKit = e.Value == 1;
            playerEntity.FindProperty("m_bHasHelmet").IntRecived += (_, e) => p.HasHelmet = e.Value == 1;
            playerEntity.FindProperty("localdata.m_Local.m_bDucking").IntRecived +=
                (_, e) => p.IsDucking = e.Value == 1;

            playerEntity.FindProperty("m_iAccount").IntRecived += (_, e) => p.Money = e.Value;
            playerEntity.FindProperty("m_angEyeAngles[0]").FloatRecived += (_, e) => p.ViewDirectionX = e.Value;
            playerEntity.FindProperty("m_angEyeAngles[1]").FloatRecived += (_, e) => p.ViewDirectionY = e.Value;
            playerEntity.FindProperty("m_flFlashDuration").FloatRecived += (_, e) => p.FlashDuration = e.Value;

            playerEntity.FindProperty("localdata.m_vecVelocity[0]").FloatRecived += (_, e) => p.Velocity.X = e.Value;

            playerEntity.FindProperty("localdata.m_vecVelocity[1]").FloatRecived += (_, e) => p.Velocity.Y = e.Value;

            playerEntity.FindProperty("localdata.m_vecVelocity[2]").FloatRecived += (_, e) => p.Velocity.Z = e.Value;

            playerEntity.FindProperty("m_unCurrentEquipmentValue").IntRecived +=
                (_, e) => p.CurrentEquipmentValue = e.Value;

            playerEntity.FindProperty("m_unRoundStartEquipmentValue").IntRecived +=
                (_, e) => p.RoundStartEquipmentValue = e.Value;

            playerEntity.FindProperty("m_unFreezetimeEndEquipmentValue").IntRecived +=
                (_, e) => p.FreezetimeEndEquipmentValue = e.Value;

            //Weapon attribution
            string weaponPrefix = "m_hMyWeapons.";

            if (playerEntity.Props.All(a => a.Entry.PropertyName != "m_hMyWeapons.000"))
                weaponPrefix = "bcc_nonlocaldata.m_hMyWeapons.";

            int[] cache = new int[MAXWEAPONS];

            for (int i = 0; i < MAXWEAPONS; i++)
            {
                int iForTheMethod = i; //Because else i is passed as reference to the delegate.

                playerEntity.FindProperty(weaponPrefix + i.ToString().PadLeft(3, '0')).IntRecived += (_, e) =>
                {
                    int index = e.Value & INDEX_MASK;

                    if (index != INDEX_MASK)
                    {
                        if (cache[iForTheMethod] != 0) //Player already has a weapon in this slot.
                        {
                            p.rawWeapons.Remove(cache[iForTheMethod]);
                            cache[iForTheMethod] = 0;
                        }

                        cache[iForTheMethod] = index;

                        AttributeWeapon(index, p);
                    }
                    else
                    {
                        if (cache[iForTheMethod] != 0 && p.rawWeapons.ContainsKey(cache[iForTheMethod]))
                            p.rawWeapons[cache[iForTheMethod]].Owner = null;

                        p.rawWeapons.Remove(cache[iForTheMethod]);

                        cache[iForTheMethod] = 0;
                    }
                };
            }

            playerEntity.FindProperty("m_hActiveWeapon").IntRecived +=
                (_, e) => p.ActiveWeaponID = e.Value & INDEX_MASK;

            for (int i = 0; i < 32; i++)
            {
                int iForTheMethod = i;

                playerEntity.FindProperty("m_iAmmo." + i.ToString().PadLeft(3, '0')).IntRecived += (_, e) =>
                {
                    p.AmmoLeft[iForTheMethod] = e.Value;
                };
            }
        }

        private void MapEquipment()
        {
            foreach (ServerClass sc in SendTableParser.ServerClasses)
            {
                if (sc.BaseClasses.ElementAtOrDefault(6)?.Name != "CWeaponCSBase") continue;

                // It is a "weapon" (Gun, C4, ... (...is the cz still a "weapon" after the nerf? (fml, it was buffed again)))

                switch (sc.BaseClasses.ElementAtOrDefault(7)?.Name)
                {
                    case "CWeaponCSBaseGun":
                    {
                        // it is a ratatatata-weapon.
                        var name = sc.DTName.Substring("DT_Weapon".Length).ToLower();
                        equipmentMapping.Add(sc, Equipment.MapEquipment(name));
                        continue;
                    }
                    case "CBaseCSGrenade":
                    {
                        // "boom"-weapon.
                        var name = sc.DTName.Substring("DT_".Length).ToLower();
                        equipmentMapping.Add(sc, Equipment.MapEquipment(name));
                        continue;
                    }
                }

                // Check the name iff the above switch matches nothing. (usually only things that the player can hold that are neither a weapon nor a grenade (?))
                switch (sc.Name)
                {
                    case "CC4":
                        // Bomb is neither "ratatata" nor "boom", its "booooooom".
                        equipmentMapping.Add(sc, EquipmentElement.Bomb);
                        break;
                    case "CKnife":
                    case "CKnifeGG":
                        // tsching weapon
                        equipmentMapping.Add(sc, EquipmentElement.Knife);
                        break;
                    case "CWeaponNOVA":
                    case "CWeaponSawedoff":
                    case "CWeaponXM1014":
                        var name = sc.Name.Substring("CWeapon".Length).ToLower();
                        equipmentMapping.Add(sc, Equipment.MapEquipment(name));
                        break;
                    case "CBreachCharge":
                        equipmentMapping.Add(sc, EquipmentElement.BreachCharge);
                        break;
                    case "CItem_Healthshot":
                        equipmentMapping.Add(sc, EquipmentElement.HealthShot);
                        break;
                    case "CFists":
                        equipmentMapping.Add(sc, EquipmentElement.Fists);
                        break;
                    case "CMelee":
                        equipmentMapping.Add(sc, EquipmentElement.Melee);
                        break;
                    case "CTablet":
                        equipmentMapping.Add(sc, EquipmentElement.Tablet);
                        break;
                    case "CBumpMine":
                        equipmentMapping.Add(sc, EquipmentElement.BumpMine);
                        break;
                }
            }
        }

        private void AttributeWeapon(int weaponEntityIndex, Player p)
        {
            Equipment weapon = weapons[weaponEntityIndex];
            weapon.Owner = p;
            p.rawWeapons[weaponEntityIndex] = weapon;
        }

        private void HandleWeapons()
        {
            for (int i = 0; i < MAX_ENTITIES; i++)
                weapons[i] = new Equipment();

            foreach (ServerClass s in SendTableParser.ServerClasses.Where(
                a => a.BaseClasses.Any(c => c.Name == "CWeaponCSBase")
            ))
                s.OnNewEntity += HandleWeapon;
        }

        private void HandleWeapon(object sender, EntityCreatedEventArgs e)
        {
            Equipment equipment = weapons[e.Entity.ID];
            equipment.EntityID = e.Entity.ID;
            equipment.Weapon = equipmentMapping[e.Class];
            equipment.AmmoInMagazine = -1;

            e.Entity.FindProperty("m_iClip1").IntRecived += (_, ammoUpdate) =>
            {
                equipment.AmmoInMagazine = ammoUpdate.Value - 1; //wtf volvo y -1?
            };

            e.Entity.FindProperty("LocalWeaponData.m_iPrimaryAmmoType").IntRecived += (_, typeUpdate) =>
            {
                equipment.AmmoType = typeUpdate.Value;
            };

            if (equipment.Weapon == EquipmentElement.P2000)
                e.Entity.FindProperty("m_nModelIndex").IntRecived += (_, e2) =>
                {
                    equipment.OriginalString = modelprecache[e2.Value];
                    if (modelprecache[e2.Value].Contains("_pist_223"))
                        equipment.Weapon = EquipmentElement.USP; //BAM
                    else if (modelprecache[e2.Value].Contains("_pist_hkp2000"))
                        equipment.Weapon = EquipmentElement.P2000;
                    else
                        throw new InvalidDataException("Unknown weapon model");
                };

            if (equipment.Weapon == EquipmentElement.M4A4)
                e.Entity.FindProperty("m_nModelIndex").IntRecived += (_, e2) =>
                {
                    equipment.OriginalString = modelprecache[e2.Value];
                    if (modelprecache[e2.Value].Contains("_rif_m4a1_s"))
                        equipment.Weapon = EquipmentElement.M4A1; //BAM

                    // if it's not an M4A1-S, check if it's an M4A4
                    else if (modelprecache[e2.Value].Contains("_rif_m4a1"))
                        equipment.Weapon = EquipmentElement.M4A4;
                    else
                        throw new InvalidDataException("Unknown weapon model");
                };

            if (equipment.Weapon == EquipmentElement.P250)
                e.Entity.FindProperty("m_nModelIndex").IntRecived += (_, e2) =>
                {
                    equipment.OriginalString = modelprecache[e2.Value];
                    if (modelprecache[e2.Value].Contains("_pist_cz_75"))
                        equipment.Weapon = EquipmentElement.CZ; //BAM
                    else if (modelprecache[e2.Value].Contains("_pist_p250"))
                        equipment.Weapon = EquipmentElement.P250;
                    else
                        throw new InvalidDataException("Unknown weapon model");
                };

            if (equipment.Weapon == EquipmentElement.Deagle)
                e.Entity.FindProperty("m_nModelIndex").IntRecived += (_, e2) =>
                {
                    equipment.OriginalString = modelprecache[e2.Value];
                    if (modelprecache[e2.Value].Contains("_pist_deagle"))
                        equipment.Weapon = EquipmentElement.Deagle; //BAM
                    else if (modelprecache[e2.Value].Contains("_pist_revolver"))
                        equipment.Weapon = EquipmentElement.Revolver;
                    else
                        throw new InvalidDataException("Unknown weapon model");
                };

            if (equipment.Weapon == EquipmentElement.MP7)
                e.Entity.FindProperty("m_nModelIndex").IntRecived += (_, e2) =>
                {
                    equipment.OriginalString = modelprecache[e2.Value];
                    if (modelprecache[e2.Value].Contains("_smg_mp7"))
                        equipment.Weapon = EquipmentElement.MP7;
                    else if (modelprecache[e2.Value].Contains("_smg_mp5sd"))
                        equipment.Weapon = EquipmentElement.MP5SD;
                    else
                        throw new InvalidDataException("Unknown weapon model");
                };
        }

        public void HandleBombSitesAndRescueZones()
        {
            HashSet<int> rescueZoneIdsDoneAtLeastOnceMin = new(), rescueZoneIdsDoneAtLeastOnceMax = new();

            SendTableParser.FindByName("CCSPlayerResource").OnNewEntity += (_, newResource) =>
            {
                // defuse
                newResource.Entity.FindProperty("m_bombsiteCenterA").VectorRecived += (_, center) =>
                {
                    bombsiteACenter = center.Value;
                };

                newResource.Entity.FindProperty("m_bombsiteCenterB").VectorRecived += (_, center) =>
                {
                    bombsiteBCenter = center.Value;
                };

                // hostage (for multiple hostage rescue zones it uses 000, 001, 002 & 003 (how many of them depends on value of numOfHostageRescueZones))
                int numOfSortedRescueZonesX = 0, numOfSortedZonesRescueY = 0, numOfSortedZonesRescueZ = 0;

                for (int i = 0; i < numOfHostageRescueZonesLookingFor; i++)
                {
                    newResource.Entity.FindProperty("m_hostageRescueX.00" + i).IntRecived += (_, center) =>
                    {
                        if (!rescueZoneCenters.ContainsKey(numOfSortedRescueZonesX))
                            rescueZoneCenters.Add(numOfSortedRescueZonesX, new Vector());

                        // make sure that there are values before saying it is sorted, as it will often run through with 0 values first
                        if (!(rescueZoneCenters[numOfSortedRescueZonesX].X == 0 && center.Value == 0))
                        {
                            rescueZoneCenters[numOfSortedRescueZonesX].X = center.Value;
                            numOfSortedRescueZonesX++;
                        }
                    };

                    newResource.Entity.FindProperty("m_hostageRescueY.00" + i).IntRecived += (_, center) =>
                    {
                        if (!rescueZoneCenters.ContainsKey(numOfSortedZonesRescueY))
                            rescueZoneCenters.Add(numOfSortedZonesRescueY, new Vector());

                        // make sure that there are values before saying it is sorted, as it will often run through with 0 values first
                        if (!(rescueZoneCenters[numOfSortedZonesRescueY].Y == 0 && center.Value == 0))
                        {
                            rescueZoneCenters[numOfSortedZonesRescueY].Y = center.Value;
                            numOfSortedZonesRescueY++;
                        }
                    };

                    newResource.Entity.FindProperty("m_hostageRescueZ.00" + i).IntRecived += (_, center) =>
                    {
                        if (!rescueZoneCenters.ContainsKey(numOfSortedZonesRescueZ))
                            rescueZoneCenters.Add(numOfSortedZonesRescueZ, new Vector());

                        // make sure that there are values before saying it is sorted, as it will often run through with 0 values first
                        if (!(rescueZoneCenters[numOfSortedZonesRescueZ].Z == 0 && center.Value == 0))
                        {
                            rescueZoneCenters[numOfSortedZonesRescueZ].Z = center.Value;
                            numOfSortedZonesRescueZ++;
                        }
                    };
                }
            };

            SendTableParser.FindByName("CBaseTrigger").OnNewEntity += (_, newResource) =>
            {
                var trigger = new BoundingBox();
                Triggers.Add(newResource.Entity.ID, trigger);

                // if bombsites, it gets x,y,z values from the world origin (0,0,0)
                // if hostage rescue zones, it gets x,y,z values relative to the entity's origin
                newResource.Entity.FindProperty("m_Collision.m_vecMins").VectorRecived += (_, vector) =>
                {
                    if (bombsiteACenter.Absolute == 0 && bombsiteBCenter.Absolute == 0) // is hostage or danger zone
                    {
                        rescueZoneIdsDoneAtLeastOnceMin.Add(newResource.Entity.ID);
                        trigger.Min = new Vector
                        {
                            X = rescueZoneCenters[rescueZoneIdsDoneAtLeastOnceMin.Count - 1].X + vector.Value.X,
                            Y = rescueZoneCenters[rescueZoneIdsDoneAtLeastOnceMin.Count - 1].Y + vector.Value.Y,
                            Z = rescueZoneCenters[rescueZoneIdsDoneAtLeastOnceMin.Count - 1].Z + vector.Value.Z,
                        };
                    }
                    else // is defuse
                    {
                        trigger.Min = vector.Value;
                    }
                };

                newResource.Entity.FindProperty("m_Collision.m_vecMaxs").VectorRecived += (_, vector) =>
                {
                    if (bombsiteACenter.Absolute == 0 && bombsiteBCenter.Absolute == 0) // is hostage or danger zone
                    {
                        rescueZoneIdsDoneAtLeastOnceMax.Add(newResource.Entity.ID);
                        trigger.Max = new Vector
                        {
                            X = rescueZoneCenters[rescueZoneIdsDoneAtLeastOnceMax.Count - 1].X + vector.Value.X,
                            Y = rescueZoneCenters[rescueZoneIdsDoneAtLeastOnceMax.Count - 1].Y + vector.Value.Y,
                            Z = rescueZoneCenters[rescueZoneIdsDoneAtLeastOnceMax.Count - 1].Z + vector.Value.Z,
                        };
                    }
                    else // is defuse
                    {
                        trigger.Max = vector.Value;
                    }
                };
            };
        }

        private void HandleInfernos()
        {
            ServerClass inferno = SendTableParser.FindByName("CInferno");

            inferno.OnNewEntity += (_, infEntity) =>
            {
                infEntity.Entity.FindProperty("m_hOwnerEntity").IntRecived += (_, handleID) =>
                {
                    int playerEntityID = handleID.Value & INDEX_MASK;
                    if (playerEntityID < PlayerInformations.Length && PlayerInformations[playerEntityID - 1] != null)
                        InfernoOwners[infEntity.Entity.ID] = PlayerInformations[playerEntityID - 1];
                };
            };

            inferno.OnDestroyEntity += (_, infEntity) => { InfernoOwners.Remove(infEntity.Entity.ID); };
        }
        #if SAVE_PROP_VALUES
        [Obsolete("This method is only for debugging-purposes and should never be used in production, so you need to live with this warning.")]
        public string DumpAllEntities()
        {
            var res = new StringBuilder();

            for (int i = 0; i < MAX_ENTITIES; i++)
            {
                Entity entity = Entities[i];

                if (entity == null)
                    continue;

                res.AppendFormat("Entity {0}: {1} (inherits: )", i.ToString(), entity.ServerClass.Name);

                //The class with the lowest order is the first
                //But we obv. want the highest order first :D
                foreach (ServerClass c in entity.ServerClass.BaseClasses.Reverse<ServerClass>())
                {
                    res.Append(c.Name);
                    res.Append("; ");
                }

                res.AppendLine(")");

                foreach (PropertyEntry prop in entity.Props)
                {
                    res.Append(prop.Entry.PropertyName.PadLeft(50));
                    res.Append(" = ");
                    res.Append(prop.Value);
                    res.AppendLine();
                }
            }

            return res.ToString();
        }

        [Obsolete("This method is only for debugging-purposes and shuld never be used in production, so you need to live with this warning.")]
        public void DumpAllEntities(string fileName)
        {
            StreamWriter writer = new StreamWriter(fileName);
            writer.WriteLine(DumpAllEntities());
            writer.Flush();
            writer.Close();
        }
        #endif

        #region EventCaller

        internal void RaisePlayerPositions(PlayerPositionsEventArgs playerPositionsEventArgs)
        {
            PlayerPositions?.Invoke(this, playerPositionsEventArgs);
        }

        internal void RaiseMatchStarted(MatchStartedEventArgs matchStartedEventArgs)
        {
            MatchStarted?.Invoke(this, matchStartedEventArgs);
        }

        internal void RaiseRoundAnnounceMatchStarted()
        {
            RoundAnnounceMatchStarted?.Invoke(this, new RoundAnnounceMatchStartedEventArgs());
        }

        internal void RaiseWinPanelMatch()
        {
            WinPanelMatch?.Invoke(this, new WinPanelMatchEventArgs());
        }

        internal void RaiseRoundStart(RoundStartedEventArgs rs)
        {
            RoundStart?.Invoke(this, rs);
        }

        internal void RaiseRoundFinal()
        {
            RoundFinal?.Invoke(this, new RoundFinalEventArgs());
        }

        internal void RaiseLastRoundHalf()
        {
            LastRoundHalf?.Invoke(this, new LastRoundHalfEventArgs());
        }

        public void RaiseRoundEnd(RoundEndedEventArgs re)
        {
            RoundEnd?.Invoke(this, re);
        }

        internal void RaiseSwitchSides()
        {
            SwitchSides?.Invoke(this, new SwitchSidesEventArgs());
        }

        public void RaiseRoundOfficiallyEnded(RoundOfficiallyEndedEventArgs roe)
        {
            RoundOfficiallyEnded?.Invoke(this, roe);
        }

        internal void RaiseRoundMVP(RoundMVPEventArgs re)
        {
            RoundMVP?.Invoke(this, re);
        }

        public void RaiseFreezetimeEnded(FreezetimeEndedEventArgs fe)
        {
            FreezetimeEnded?.Invoke(this, fe);
        }

        internal void RaiseOtherKilled()
        {
            OtherKilled?.Invoke(this, new OtherKilledEventArgs());
        }

        internal void RaiseChickenKilled()
        {
            ChickenKilled?.Invoke(this, new ChickenKilledEventArgs());
        }

        public void RaisePlayerKilled(PlayerKilledEventArgs kill)
        {
            PlayerKilled?.Invoke(this, kill);
        }

        internal void RaisePlayerHurt(PlayerHurtEventArgs hurt)
        {
            PlayerHurt?.Invoke(this, hurt);
        }

        internal void RaiseBlind(BlindEventArgs blind)
        {
            Blind?.Invoke(this, blind);
        }

        internal void RaisePlayerBind(PlayerBindEventArgs bind)
        {
            PlayerBind?.Invoke(this, bind);
        }

        internal void RaisePlayerDisconnect(PlayerDisconnectEventArgs bind)
        {
            PlayerDisconnect?.Invoke(this, bind);
        }

        internal void RaisePlayerTeam(PlayerTeamEventArgs args)
        {
            PlayerTeam?.Invoke(this, args);
        }

        internal void RaiseBotTakeOver(BotTakeOverEventArgs take)
        {
            BotTakeOver?.Invoke(this, take);
        }

        internal void RaiseWeaponFired(WeaponFiredEventArgs fire)
        {
            WeaponFired?.Invoke(this, fire);
        }

        internal void RaiseSmokeStart(SmokeEventArgs args)
        {
            SmokeNadeStarted?.Invoke(this, args);

            NadeReachedTarget?.Invoke(this, args);
        }

        internal void RaiseSmokeEnd(SmokeEventArgs args)
        {
            SmokeNadeEnded?.Invoke(this, args);
        }

        internal void RaiseDecoyStart(DecoyEventArgs args)
        {
            DecoyNadeStarted?.Invoke(this, args);

            NadeReachedTarget?.Invoke(this, args);
        }

        internal void RaiseDecoyEnd(DecoyEventArgs args)
        {
            DecoyNadeEnded?.Invoke(this, args);
        }

        internal void RaiseFireStart(FireEventArgs args)
        {
            FireNadeStarted?.Invoke(this, args);

            NadeReachedTarget?.Invoke(this, args);
        }

        internal void RaiseFireWithOwnerStart(FireEventArgs args)
        {
            FireNadeWithOwnerStarted?.Invoke(this, args);

            NadeReachedTarget?.Invoke(this, args);
        }

        internal void RaiseFireEnd(FireEventArgs args)
        {
            FireNadeEnded?.Invoke(this, args);
        }

        internal void RaiseFlashExploded(FlashEventArgs args)
        {
            FlashNadeExploded?.Invoke(this, args);

            NadeReachedTarget?.Invoke(this, args);
        }

        internal void RaiseGrenadeExploded(GrenadeEventArgs args)
        {
            ExplosiveNadeExploded?.Invoke(this, args);

            NadeReachedTarget?.Invoke(this, args);
        }

        internal void RaiseBombBeginPlant(BombEventArgs args)
        {
            BombBeginPlant?.Invoke(this, args);
        }

        internal void RaiseBombAbortPlant(BombEventArgs args)
        {
            BombAbortPlant?.Invoke(this, args);
        }

        internal void RaiseBombPlanted(BombEventArgs args)
        {
            BombPlanted?.Invoke(this, args);
        }

        internal void RaiseBombDefused(BombEventArgs args)
        {
            BombDefused?.Invoke(this, args);
        }

        internal void RaiseBombExploded(BombEventArgs args)
        {
            BombExploded?.Invoke(this, args);
        }

        internal void RaiseBombBeginDefuse(BombDefuseEventArgs args)
        {
            BombBeginDefuse?.Invoke(this, args);
        }

        internal void RaiseBombAbortDefuse(BombDefuseEventArgs args)
        {
            BombAbortDefuse?.Invoke(this, args);
        }

        internal void RaiseHostageRescued(HostageRescuedEventArgs args)
        {
            HostageRescued?.Invoke(this, args);
        }

        internal void RaiseHostagePickedUp(HostagePickedUpEventArgs args)
        {
            HostagePickedUp?.Invoke(this, args);
        }

        internal void RaiseSayText(SayTextEventArgs args)
        {
            SayText?.Invoke(this, args);
        }

        internal void RaiseSayText2(SayText2EventArgs args)
        {
            SayText2?.Invoke(this, args);
        }

        internal void RaiseRankUpdate(RankUpdateEventArgs args)
        {
            RankUpdate?.Invoke(this, args);
        }

        internal void RaiseServerInfo(ServerInfoEventArgs args)
        {
            ServerInfo?.Invoke(this, args);
        }

        #endregion

        /// <summary>
        /// Releases all resource used by the <see cref="DemoParser" /> object. This must be called or evil things (memory leaks)
        /// happen.
        /// Sorry for that - I've debugged and I don't know why this is, but I can't fix it somehow.
        /// This is bad, I know.
        /// </summary>
        /// <remarks>
        /// Call <see cref="Dispose" /> when you are finished using the <see cref="DemoParser" />. The
        /// <see cref="Dispose" /> method leaves the <see cref="DemoParser" /> in an unusable state. After calling
        /// <see cref="Dispose" />, you must release all references to the <see cref="DemoParser" /> so the garbage
        /// collector can reclaim the memory that the <see cref="DemoParser" /> was occupying.
        /// </remarks>
        public void Dispose()
        {
            BitStream.Dispose();

            foreach (Entity entity in Entities)
                entity?.Leave();

            foreach (ServerClass serverClass in SendTableParser.ServerClasses)
                serverClass.Dispose();

            TickDone = null;
            BombAbortDefuse = null;
            BombAbortPlant = null;
            BombBeginDefuse = null;
            BombBeginPlant = null;
            BombDefused = null;
            BombExploded = null;
            BombPlanted = null;
            ChickenKilled = null;
            DecoyNadeEnded = null;
            DecoyNadeStarted = null;
            ExplosiveNadeExploded = null;
            FireNadeEnded = null;
            FireNadeStarted = null;
            FireNadeWithOwnerStarted = null;
            FlashNadeExploded = null;
            HeaderParsed = null;
            HostageRescued = null;
            MatchStarted = null;
            NadeReachedTarget = null;
            PlayerKilled = null;
            PlayerPositions = null;
            OtherKilled = null;
            RoundStart = null;
            SayText = null;
            SayText2 = null;
            SmokeNadeEnded = null;
            SmokeNadeStarted = null;
            SwitchSides = null;
            WeaponFired = null;

            Players.Clear();
        }
    }
}
