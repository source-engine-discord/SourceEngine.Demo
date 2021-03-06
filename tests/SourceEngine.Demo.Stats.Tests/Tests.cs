using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Shouldly;

using SourceEngine.Demo.Parser;
using SourceEngine.Demo.Parser.Constants;
using SourceEngine.Demo.Stats.Models;

using Xunit;

namespace SourceEngine.Demo.Stats.Tests
{
    public class TopStatsWaffleTests
    {
        protected DemoParser DemoParser;
        protected readonly MatchData MatchData;
        protected ProcessedData ProcessedData;

        public TopStatsWaffleTests()
        {
            MatchData = new MatchData();
            MockData();

            foreach (TeamPlayers teamPlayers in ProcessedData.TeamPlayersValues)
            {
                foreach (Player player in teamPlayers.Terrorists)
                    MatchData.BindPlayer(player);

                foreach (Player player in teamPlayers.CounterTerrorists)
                    MatchData.BindPlayer(player);
            }
        }

        public void MockData()
        {
            var DemoInformation = new DemoInformation
            {
                DemoName = "demo1",
                MapName = "de_testmap",
                GameMode = Gamemodes.Defuse,
                TestType = "casual",
                TestDate = new DateTime(2020, 1, 1, 0, 0, 0).ToString(),
            };

            var tanookiStats = new tanookiStats
            {
                Joined = true,
                Left = true,
                RoundJoined = 1,
                RoundLeft = 2,
                RoundsLasted = 1,
            };

            var MatchStartValues = new List<MatchStartedEventArgs>
            {
                new()
                {
                    Mapname = "de_testmap",
                    HasBombsites = true,
                },
            };

            var SwitchSidesValues = new List<SwitchSidesEventArgs>
            {
                new()
                {
                    RoundBeforeSwitch = 1,
                },
            };

            var MessagesValues = new List<FeedbackMessage>
            {
                new()
                {
                    Round = 1,
                    SteamID = 12321313213,
                    TeamName = "AlphaTeam",
                    XCurrentPosition = 50,
                    YCurrentPosition = 60,
                    ZCurrentPosition = 70,
                    XLastAlivePosition = 120,
                    YLastAlivePosition = 130,
                    ZLastAlivePosition = 140,
                    XCurrentViewAngle = 45.0f,
                    YCurrentViewAngle = 225.0f,
                    SetPosCommandCurrentPosition = "setpos 50 60 70; setang 45 225",
                    Message = "bad map",
                    TimeInRound = 31.7568,
                },
            };

            var TeamPlayersValues = new List<TeamPlayers>
            {
                new()
                {
                    Round = 1,
                    Terrorists = new List<Player>
                    {
                        new()
                        {
                            Name = "JimWood",
                            SteamID = 32443298432,
                            Team = Team.Terrorist,
                            EntityID = 45,
                            UserID = 1,
                            LastAlivePosition = new Vector
                            {
                                X = 100,
                                Y = 100,
                                Z = 100,
                            },
                            Position = new Vector
                            {
                                X = 200,
                                Y = 200,
                                Z = 200,
                            },
                            Money = 200,
                            RoundStartEquipmentValue = 2700,
                        },
                    },
                    CounterTerrorists = new List<Player>
                    {
                        new()
                        {
                            Name = "TheWhaleMan",
                            SteamID = 12321313213,
                            Team = Team.CounterTerrorist,
                            EntityID = 46,
                            UserID = 2,
                            LastAlivePosition = new Vector
                            {
                                X = 90,
                                Y = 900,
                                Z = 9000,
                            },
                            Position = new Vector
                            {
                                X = 80,
                                Y = 800,
                                Z = 8000,
                            },
                            Money = 200,
                            RoundStartEquipmentValue = 200,
                        },
                    },
                },
                new()
                {
                    Round = 2,
                    Terrorists = new List<Player>
                    {
                        new()
                        {
                            Name = "TheWhaleMan",
                            SteamID = 12321313213,
                            EntityID = 46,
                            UserID = 2,
                            LastAlivePosition = new Vector
                            {
                                X = 400,
                                Y = 400,
                                Z = 400,
                            },
                            Position = new Vector
                            {
                                X = 500,
                                Y = 500,
                                Z = 500,
                            },
                            Money = 1000,
                            RoundStartEquipmentValue = 200,
                        },
                    },
                    CounterTerrorists = new List<Player>
                    {
                        new()
                        {
                            Name = "JimWood",
                            SteamID = 32443298432,
                            EntityID = 45,
                            UserID = 1,
                            LastAlivePosition = new Vector
                            {
                                X = 70,
                                Y = 70,
                                Z = 70,
                            },
                            Position = new Vector
                            {
                                X = 60,
                                Y = 60,
                                Z = 60,
                            },
                            Money = 5000,
                            RoundStartEquipmentValue = 4750,
                        },
                    },
                },
            };

            var PlayerHurtValues = new List<PlayerHurt>
            {
                new()
                {
                    Round = 1,
                    TimeInRound = 40,
                    Player = TeamPlayersValues[0].CounterTerrorists[0],
                    Attacker = TeamPlayersValues[0].Terrorists[0],
                    Health = 0,
                    Armor = 50,
                    Weapon = new Equipment("weapon_ak47"),
                    HealthDamage = 100,
                    ArmorDamage = 50,
                    Hitgroup = Hitgroup.Head,
                    PossiblyKilledByBombExplosion = false,
                },
                new()
                {
                    Round = 2,
                    TimeInRound = 90,
                    Player = TeamPlayersValues[1].Terrorists[0],
                    Attacker = TeamPlayersValues[1].CounterTerrorists[0],
                    Health = 0,
                    Armor = 25,
                    Weapon = new Equipment("weapon_awp"),
                    HealthDamage = 150,
                    ArmorDamage = 75,
                    Hitgroup = Hitgroup.Head,
                    PossiblyKilledByBombExplosion = false,
                },
            };

            var PlayerKilledEventsValues = new List<PlayerKilledEventArgs>
            {
                new()
                {
                    Round = 1,
                    TimeInRound = 40,
                    Killer = TeamPlayersValues[0].Terrorists[0],
                    Victim = TeamPlayersValues[0].CounterTerrorists[0],
                    Assister = null,
                    KillerBotTakeover = false,
                    VictimBotTakeover = false,
                    AssisterBotTakeover = false,
                    Headshot = true,
                    Suicide = false,
                    TeamKill = false,
                    PenetratedObjects = 0,
                    AssistedFlash = false,
                },
                new()
                {
                    Round = 2,
                    TimeInRound = 90,
                    Killer = TeamPlayersValues[1].CounterTerrorists[0],
                    Victim = TeamPlayersValues[1].Terrorists[0],
                    Assister = null,
                    KillerBotTakeover = true,
                    VictimBotTakeover = true,
                    AssisterBotTakeover = true,
                    Headshot = true,
                    Suicide = false,
                    TeamKill = false,
                    PenetratedObjects = 1,
                    AssistedFlash = true,
                },
            };

            var PlayerValues = new Dictionary<string, IEnumerable<Player>>
            {
                {
                    "Kills", new List<Player>
                    {
                        TeamPlayersValues[0].Terrorists[0],
                        TeamPlayersValues[1].CounterTerrorists[0],
                    }
                },
                {
                    "Deaths", new List<Player>
                    {
                        TeamPlayersValues[0].CounterTerrorists[0],
                        TeamPlayersValues[1].Terrorists[0],
                    }
                },
                {
                    "Headshots", new List<Player>
                    {
                        TeamPlayersValues[0].Terrorists[0],
                    }
                },
                {
                    "Assists", new List<Player>
                    {
                        TeamPlayersValues[0].CounterTerrorists[0],
                    }
                },
                {
                    "MVPs", new List<Player>
                    {
                        TeamPlayersValues[0].Terrorists[0],
                        TeamPlayersValues[1].CounterTerrorists[0],
                    }
                },
                {
                    "Shots", new List<Player>
                    {
                        TeamPlayersValues[0].Terrorists[0],
                        TeamPlayersValues[0].Terrorists[0],
                        TeamPlayersValues[0].Terrorists[0],
                        TeamPlayersValues[1].Terrorists[0],
                        TeamPlayersValues[1].CounterTerrorists[0],
                        TeamPlayersValues[1].CounterTerrorists[0],
                        TeamPlayersValues[1].CounterTerrorists[0],
                    }
                },
                {
                    "Plants", new List<Player>
                    {
                        TeamPlayersValues[0].Terrorists[0],
                        TeamPlayersValues[1].Terrorists[0],
                    }
                },
                {
                    "Defuses", new List<Player>
                    {
                        TeamPlayersValues[1].CounterTerrorists[0],
                    }
                },
                {
                    "Rescues", new List<Player>
                    {
                        TeamPlayersValues[0].CounterTerrorists[0],
                        TeamPlayersValues[0].CounterTerrorists[0],
                    }
                },
            };

            var WeaponValues = new List<Equipment>
            {
                new()
                {
                    Owner = TeamPlayersValues[0].Terrorists[0],
                    Weapon = EquipmentElement.AK47,
                },
                new()
                {
                    Owner = TeamPlayersValues[0].CounterTerrorists[0],
                    Weapon = EquipmentElement.AWP,
                },
            };

            var PenetrationValues = new List<int>
            {
                0,
                1,
            };

            var BombsitePlantValues = new List<BombPlanted>
            {
                new()
                {
                    Bombsite = 'A',
                    Player = TeamPlayersValues[0].Terrorists[0],
                    Round = 1,
                    TimeInRound = 35,
                    XPosition = 100,
                    YPosition = 100,
                    ZPosition = 100,
                },
                new()
                {
                    Bombsite = 'B',
                    Player = TeamPlayersValues[1].Terrorists[0],
                    Round = 2,
                    TimeInRound = 60,
                    XPosition = 400,
                    YPosition = 400,
                    ZPosition = 400,
                },
            };

            var BombsiteExplodeValues = new List<BombExploded>
            {
                new()
                {
                    Bombsite = 'A',
                    Player = TeamPlayersValues[0].Terrorists[0],
                    Round = 1,
                    TimeInRound = 75,
                },
            };

            var BombsiteDefuseValues = new List<BombDefused>
            {
                new()
                {
                    Bombsite = 'B',
                    Player = TeamPlayersValues[1].CounterTerrorists[0],
                    Round = 2,
                    TimeInRound = 100,
                    HasKit = true,
                },
            };

            var HostageRescueValues = new List<HostageRescued>
            {
                new()
                {
                    Hostage = 'A',
                    HostageIndex = 250,
                    RescueZone = 0,
                    Player = TeamPlayersValues[0].CounterTerrorists[0],
                    Round = 1,
                    TimeInRound = 50,
                    XPosition = 800,
                    YPosition = 800,
                    ZPosition = 800,
                },
                new()
                {
                    Hostage = 'B',
                    HostageIndex = 251,
                    RescueZone = 0,
                    Player = TeamPlayersValues[0].CounterTerrorists[0],
                    Round = 1,
                    TimeInRound = 51,
                    XPosition = 700,
                    YPosition = 700,
                    ZPosition = 700,
                },
            };

            var HostagePickedUpValues = new List<HostagePickedUp>
            {
                new()
                {
                    Hostage = 'A',
                    HostageIndex = 250,
                    Player = TeamPlayersValues[0].CounterTerrorists[0],
                    Round = 1,
                    TimeInRound = 20,
                },
                new()
                {
                    Hostage = 'B',
                    HostageIndex = 251,
                    Player = TeamPlayersValues[0].CounterTerrorists[0],
                    Round = 1,
                    TimeInRound = 35,
                },
                new()
                {
                    Hostage = 'A',
                    HostageIndex = 250,
                    Player = TeamPlayersValues[1].CounterTerrorists[0],
                    Round = 2,
                    TimeInRound = 40,
                },
            };

            var TeamValues = new List<Team>
            {
                Team.Terrorist,
                Team.CounterTerrorist,
            };

            var RoundEndReasonValues = new List<RoundEndReason>
            {
                RoundEndReason.TargetBombed,
                RoundEndReason.BombDefused,
            };

            var RoundLengthValues = new List<double>
            {
                80,
                105,
            };

            var TeamEquipmentValues = new List<TeamEquipment>
            {
                new()
                {
                    Round = 1,
                    TEquipValue = 2900,
                    TExpenditure = 200,
                    CTEquipValue = 450,
                    CTExpenditure = 50,
                },
                new()
                {
                    Round = 2,
                    TEquipValue = 800,
                    TExpenditure = 600,
                    CTEquipValue = 5750,
                    CTExpenditure = 1000,
                },
            };

            var GrenadeValues = new List<NadeEventArgs>
            {
                new FlashEventArgs
                {
                    NadeType = EquipmentElement.Flash,
                    ThrownBy = TeamPlayersValues[0].Terrorists[0],
                    Position = new Vector
                    {
                        X = 500,
                        Y = 500,
                        Z = 500,
                    },
                    FlashedPlayers = new[] { TeamPlayersValues[0].CounterTerrorists[0] },
                },
                new()
                {
                    NadeType = EquipmentElement.Smoke,
                    ThrownBy = TeamPlayersValues[0].Terrorists[0],
                    Position = new Vector
                    {
                        X = 500,
                        Y = 500,
                        Z = 500,
                    },
                },
                new()
                {
                    NadeType = EquipmentElement.HE,
                    ThrownBy = TeamPlayersValues[0].Terrorists[0],
                    Position = new Vector
                    {
                        X = 500,
                        Y = 500,
                        Z = 500,
                    },
                },
                new()
                {
                    NadeType =
                        EquipmentElement
                            .Molotov, // all molotovs are down as incendiaries, specified why in DemoParser.cs, search for "FireNadeStarted".
                    ThrownBy = TeamPlayersValues[0].Terrorists[0],
                    Position = new Vector
                    {
                        X = 500,
                        Y = 500,
                        Z = 500,
                    },
                },
                new()
                {
                    NadeType = EquipmentElement.Incendiary,
                    ThrownBy = TeamPlayersValues[0].Terrorists[0],
                    Position = new Vector
                    {
                        X = 500,
                        Y = 500,
                        Z = 500,
                    },
                },
                new()
                {
                    NadeType = EquipmentElement.Decoy,
                    ThrownBy = TeamPlayersValues[0].Terrorists[0],
                    Position = new Vector
                    {
                        X = 500,
                        Y = 500,
                        Z = 500,
                    },
                },
            };

            var ChickenValues = new List<ChickenKilledEventArgs> { new() };

            var ShotsFiredValues = new List<ShotFired>
            {
                new()
                {
                    Round = 1,
                    TimeInRound = 1,
                    TeamSide = Team.Terrorist.ToString(),
                    Shooter = TeamPlayersValues[0].Terrorists[0],
                },
                new()
                {
                    Round = 1,
                    TimeInRound = 1,
                    TeamSide = Team.Terrorist.ToString(),
                    Shooter = TeamPlayersValues[0].Terrorists[0],
                },
                new()
                {
                    Round = 1,
                    TimeInRound = 1,
                    TeamSide = Team.Terrorist.ToString(),
                    Shooter = TeamPlayersValues[0].Terrorists[0],
                },
                new()
                {
                    Round = 2,
                    TimeInRound = 1,
                    TeamSide = Team.Terrorist.ToString(),
                    Shooter = TeamPlayersValues[1].Terrorists[0],
                },
                new()
                {
                    Round = 2,
                    TimeInRound = 1,
                    TeamSide = Team.CounterTerrorist.ToString(),
                    Shooter = TeamPlayersValues[1].CounterTerrorists[0],
                },
                new()
                {
                    Round = 2,
                    TimeInRound = 1,
                    TeamSide = Team.CounterTerrorist.ToString(),
                    Shooter = TeamPlayersValues[1].CounterTerrorists[0],
                },
                new()
                {
                    Round = 2,
                    TimeInRound = 1,
                    TeamSide = Team.CounterTerrorist.ToString(),
                    Shooter = TeamPlayersValues[1].CounterTerrorists[0],
                },
            };

            var playerPositionsStats = new List<PlayerPositionsInstance>
            {
                new()
                {
                    Round = 1,
                    TimeInRound = 1,
                    TeamSide = Team.Terrorist.ToString(),
                    SteamID = TeamPlayersValues[0].Terrorists[0].SteamID,
                    XPosition = 20,
                    YPosition = 200,
                    ZPosition = 2000,
                },
            };

            ProcessedData = new ProcessedData
            {
                DemoInformation = DemoInformation,
                SameFilename = true,
                SameFolderStructure = true,
                ParseChickens = true,
                ParsePlayerPositions = true,
                FoldersToProcess = new List<string> { "someFolder" },
                OutputRootFolder = "outputFolder",
                tanookiStats = tanookiStats,
                MatchStartValues = MatchStartValues,
                SwitchSidesValues = SwitchSidesValues,
                MessagesValues = MessagesValues,
                TeamPlayersValues = TeamPlayersValues,
                PlayerHurtValues = PlayerHurtValues,
                PlayerKilledEventsValues = PlayerKilledEventsValues,
                PlayerValues = PlayerValues,
                WeaponValues = WeaponValues,
                PenetrationValues = PenetrationValues,
                BombsitePlantValues = BombsitePlantValues,
                BombsiteExplodeValues = BombsiteExplodeValues,
                BombsiteDefuseValues = BombsiteDefuseValues,
                HostageRescueValues = HostageRescueValues,
                HostagePickedUpValues = HostagePickedUpValues,
                TeamValues = TeamValues,
                RoundEndReasonValues = RoundEndReasonValues,
                RoundLengthValues = RoundLengthValues,
                TeamEquipmentValues = TeamEquipmentValues,
                GrenadeValues = GrenadeValues,
                ChickenValues = ChickenValues,
                ShotsFiredValues = ShotsFiredValues,
                PlayerPositionsValues = playerPositionsStats,
                WriteTicks = true,
            };
        }

        public class DataValidationTests : TopStatsWaffleTests
        {
            [Fact]
            public void Should_return_bombsite_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.bombsiteStats.Count.ShouldBe(2);
                allOutputData.AllStats.bombsiteStats[0].Plants.ShouldBe(1);
                allOutputData.AllStats.bombsiteStats[0].Explosions.ShouldBe(1);
                allOutputData.AllStats.bombsiteStats[0].Defuses.ShouldBe(0);
                allOutputData.AllStats.bombsiteStats[1].Plants.ShouldBe(1);
                allOutputData.AllStats.bombsiteStats[1].Explosions.ShouldBe(0);
                allOutputData.AllStats.bombsiteStats[1].Defuses.ShouldBe(1);
            }

            [Fact]
            public void Should_return_chicken_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.chickenStats.Killed.ShouldBe(1);
            }

            [Fact]
            public void Should_return_feedback_messages_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.feedbackMessages.Count.ShouldBe(1);
                allOutputData.AllStats.feedbackMessages[0].Round.ShouldBe(1);
                allOutputData.AllStats.feedbackMessages[0].Message.ShouldBe("bad map");
            }

            [Fact]
            public void Should_return_first_shot_stats_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.firstDamageStats.Count.ShouldBe(2);
                allOutputData.AllStats.firstDamageStats[0].Round.ShouldBe(1);
                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().TimeInRound
                    .ShouldBe(40);

                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().TeamSideShooter
                    .ShouldBe("Terrorist");

                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().SteamIDShooter
                    .ShouldBe(32443298432);

                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().XPositionShooter
                    .ShouldBe(0);

                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().YPositionShooter
                    .ShouldBe(0);

                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().ZPositionShooter
                    .ShouldBe(0);

                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().TeamSideVictim
                    .ShouldBe("CounterTerrorist");

                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().SteamIDVictim
                    .ShouldBe(12321313213);

                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().XPositionVictim
                    .ShouldBe(0);

                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().YPositionVictim
                    .ShouldBe(0);

                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().ZPositionVictim
                    .ShouldBe(0);

                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().Weapon
                    .ShouldBe("AK47");

                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().WeaponClass
                    .ShouldBe("Rifle");

                allOutputData.AllStats.firstDamageStats[0].FirstDamageToEnemyByPlayers.FirstOrDefault().WeaponType
                    .ShouldBe("AssaultRifle");
            }

            [Fact]
            public void Should_return_grenade_specific_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.grenadesSpecificStats.Count.ShouldBe(6);
                allOutputData.AllStats.grenadesSpecificStats[0].NadeType.ShouldBe(EquipmentElement.Flash.ToString());
                allOutputData.AllStats.grenadesSpecificStats[1].NadeType.ShouldBe(EquipmentElement.Smoke.ToString());
                allOutputData.AllStats.grenadesSpecificStats[2].NadeType.ShouldBe(EquipmentElement.HE.ToString());
                allOutputData.AllStats.grenadesSpecificStats[3].NadeType.ShouldBe(EquipmentElement.Molotov.ToString());
                allOutputData.AllStats.grenadesSpecificStats[4].NadeType
                    .ShouldBe(EquipmentElement.Incendiary.ToString());

                allOutputData.AllStats.grenadesSpecificStats[5].NadeType.ShouldBe(EquipmentElement.Decoy.ToString());
            }

            [Fact]
            public void Should_return_grenade_total_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.grenadesTotalStats.Count.ShouldBe(5);
                allOutputData.AllStats.grenadesTotalStats[0].NadeType.ShouldBe(EquipmentElement.Flash.ToString());
                allOutputData.AllStats.grenadesTotalStats[0].AmountUsed.ShouldBe(1);
                allOutputData.AllStats.grenadesTotalStats[1].NadeType.ShouldBe(EquipmentElement.Smoke.ToString());
                allOutputData.AllStats.grenadesTotalStats[1].AmountUsed.ShouldBe(1);
                allOutputData.AllStats.grenadesTotalStats[2].NadeType.ShouldBe(EquipmentElement.HE.ToString());
                allOutputData.AllStats.grenadesTotalStats[2].AmountUsed.ShouldBe(1);
                allOutputData.AllStats.grenadesTotalStats[3].NadeType.ShouldBe(EquipmentElement.Incendiary.ToString());
                allOutputData.AllStats.grenadesTotalStats[3].AmountUsed.ShouldBe(2);
                allOutputData.AllStats.grenadesTotalStats[4].NadeType.ShouldBe(EquipmentElement.Decoy.ToString());
                allOutputData.AllStats.grenadesTotalStats[4].AmountUsed.ShouldBe(1);
            }

            [Fact]
            public void Should_return_hostage_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.hostageStats.Count.ShouldBe(2);
                allOutputData.AllStats.hostageStats[0].Hostage.ShouldBe('A');
                allOutputData.AllStats.hostageStats[0].HostageIndex.ShouldBe(250);
                allOutputData.AllStats.hostageStats[0].PickedUps.ShouldBe(2);
                allOutputData.AllStats.hostageStats[0].Rescues.ShouldBe(1);
                allOutputData.AllStats.hostageStats[1].Hostage.ShouldBe('B');
                allOutputData.AllStats.hostageStats[1].HostageIndex.ShouldBe(251);
                allOutputData.AllStats.hostageStats[1].PickedUps.ShouldBe(1);
                allOutputData.AllStats.hostageStats[1].Rescues.ShouldBe(1);
            }

            /*[Fact]
            public void Should_return_rescue_zone_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.rescueZoneStats.Count.ShouldBe(1); // cannot test positions as is currently, as DemoParser is not implemented
            }*/

            [Fact]
            public void Should_return_kills_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.killsStats.Count.ShouldBe(2);
                allOutputData.AllStats.killsStats[0].Round.ShouldBe(1);
                allOutputData.AllStats.killsStats[0].TimeInRound.ShouldBe(40);
                allOutputData.AllStats.killsStats[1].Round.ShouldBe(2);
                allOutputData.AllStats.killsStats[1].TimeInRound.ShouldBe(90);
            }

            [Fact]
            public void Should_return_map_info_correctly_for_defuse_maps()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.mapInfo.DemoName.ShouldBe("demo1");
                allOutputData.AllStats.mapInfo.MapName.ShouldBe("de_testmap");
                allOutputData.AllStats.mapInfo.GameMode.ShouldBe(Gamemodes.Defuse);
                allOutputData.AllStats.mapInfo.TestType.ShouldBe("casual");
                allOutputData.AllStats.mapInfo.TestDate.ShouldBe(new DateTime(2020, 1, 1, 0, 0, 0).ToString());
            }

            [Fact]
            public void Should_return_map_info_correctly_for_hostage_maps()
            {
                // Arrange
                ProcessedData.DemoInformation = new DemoInformation
                {
                    DemoName = "demo2",
                    MapName = "de_testmap2",
                    GameMode = Gamemodes.Hostage,
                    TestType = "casual",
                    TestDate = new DateTime(2020, 1, 1, 0, 0, 0).ToString(),
                };

                ProcessedData.MatchStartValues = new List<MatchStartedEventArgs>
                {
                    new()
                    {
                        Mapname = "de_testmap2",
                        HasBombsites = false,
                    },
                };

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.mapInfo.DemoName.ShouldBe("demo2");
                allOutputData.AllStats.mapInfo.MapName.ShouldBe("de_testmap2");
                allOutputData.AllStats.mapInfo.GameMode.ShouldBe(Gamemodes.Hostage);
                allOutputData.AllStats.mapInfo.TestType.ShouldBe("casual");
                allOutputData.AllStats.mapInfo.TestDate.ShouldBe(new DateTime(2020, 1, 1, 0, 0, 0).ToString());
            }

            [Fact]
            public void Should_return_map_info_correctly_for_wingman_defuse_maps()
            {
                // Arrange
                ProcessedData.DemoInformation = new DemoInformation
                {
                    DemoName = "demo3",
                    MapName = "de_testmap3",
                    GameMode = Gamemodes.WingmanDefuse,
                    TestType = "casual",
                    TestDate = new DateTime(2020, 1, 1, 0, 0, 0).ToString(),
                };

                ProcessedData.MatchStartValues = new List<MatchStartedEventArgs>
                {
                    new()
                    {
                        Mapname = "de_testmap3",
                        HasBombsites = true,
                    },
                };

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.mapInfo.DemoName.ShouldBe("demo3");
                allOutputData.AllStats.mapInfo.MapName.ShouldBe("de_testmap3");
                allOutputData.AllStats.mapInfo.GameMode.ShouldBe(Gamemodes.WingmanDefuse);
                allOutputData.AllStats.mapInfo.TestType.ShouldBe("casual");
                allOutputData.AllStats.mapInfo.TestDate.ShouldBe(new DateTime(2020, 1, 1, 0, 0, 0).ToString());
            }

            [Fact]
            public void Should_return_map_info_correctly_for_wingman_hostage_maps()
            {
                // Arrange
                ProcessedData.DemoInformation = new DemoInformation
                {
                    DemoName = "demo4",
                    MapName = "de_testmap4",
                    GameMode = Gamemodes.WingmanHostage,
                    TestType = "casual",
                    TestDate = new DateTime(2020, 1, 1, 0, 0, 0).ToString(),
                };

                ProcessedData.MatchStartValues = new List<MatchStartedEventArgs>
                {
                    new()
                    {
                        Mapname = "de_testmap4",
                        HasBombsites = false,
                    },
                };

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.mapInfo.DemoName.ShouldBe("demo4");
                allOutputData.AllStats.mapInfo.MapName.ShouldBe("de_testmap4");
                allOutputData.AllStats.mapInfo.GameMode.ShouldBe(Gamemodes.WingmanHostage);
                allOutputData.AllStats.mapInfo.TestType.ShouldBe("casual");
                allOutputData.AllStats.mapInfo.TestDate.ShouldBe(new DateTime(2020, 1, 1, 0, 0, 0).ToString());
            }

            [Fact]
            public void Should_return_map_info_correctly_for_danger_zone_maps()
            {
                // Arrange
                ProcessedData.DemoInformation = new DemoInformation
                {
                    DemoName = "demo5",
                    MapName = "de_testmap5",
                    GameMode = Gamemodes.DangerZone,
                    TestType = "casual",
                    TestDate = new DateTime(2020, 1, 1, 0, 0, 0).ToString(),
                };

                ProcessedData.MatchStartValues = new List<MatchStartedEventArgs>
                {
                    new()
                    {
                        Mapname = "de_testmap5",
                        HasBombsites = false,
                    },
                };

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.mapInfo.DemoName.ShouldBe("demo5");
                allOutputData.AllStats.mapInfo.MapName.ShouldBe("de_testmap5");
                allOutputData.AllStats.mapInfo.GameMode.ShouldBe(Gamemodes.DangerZone);
                allOutputData.AllStats.mapInfo.TestType.ShouldBe("casual");
                allOutputData.AllStats.mapInfo.TestDate.ShouldBe(new DateTime(2020, 1, 1, 0, 0, 0).ToString());
            }

            [Fact]
            public void Should_return_player_positions_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.PlayerPositionsStats.PlayerPositionByRound.Count.ShouldBe(1);
                allOutputData.PlayerPositionsStats.PlayerPositionByRound.FirstOrDefault().Round.ShouldBe(1);
                allOutputData.PlayerPositionsStats.PlayerPositionByRound.FirstOrDefault().PlayerPositionByTimeInRound
                    .FirstOrDefault().TimeInRound.ShouldBe(1);

                allOutputData.PlayerPositionsStats.PlayerPositionByRound.FirstOrDefault().PlayerPositionByTimeInRound
                    .FirstOrDefault().PlayerPositionBySteamID.FirstOrDefault().SteamID.ShouldBe(32443298432);

                allOutputData.PlayerPositionsStats.PlayerPositionByRound.FirstOrDefault().PlayerPositionByTimeInRound
                    .FirstOrDefault().PlayerPositionBySteamID.FirstOrDefault().TeamSide.ShouldBe("Terrorist");

                allOutputData.PlayerPositionsStats.PlayerPositionByRound.FirstOrDefault().PlayerPositionByTimeInRound
                    .FirstOrDefault().PlayerPositionBySteamID.FirstOrDefault().XPosition.ShouldBe(20);

                allOutputData.PlayerPositionsStats.PlayerPositionByRound.FirstOrDefault().PlayerPositionByTimeInRound
                    .FirstOrDefault().PlayerPositionBySteamID.FirstOrDefault().YPosition.ShouldBe(200);

                allOutputData.PlayerPositionsStats.PlayerPositionByRound.FirstOrDefault().PlayerPositionByTimeInRound
                    .FirstOrDefault().PlayerPositionBySteamID.FirstOrDefault().ZPosition.ShouldBe(2000);
            }

            [Fact]
            public void Should_return_player_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.playerStats.Count.ShouldBe(2);

                allOutputData.AllStats.playerStats[0].Assists.ShouldBe(0);
                allOutputData.AllStats.playerStats[0].AssistsIncludingBots.ShouldBe(0);
                allOutputData.AllStats.playerStats[0].Deaths.ShouldBe(0);
                allOutputData.AllStats.playerStats[0].DeathsIncludingBots.ShouldBe(0);
                allOutputData.AllStats.playerStats[0].Defuses.ShouldBe(1);
                allOutputData.AllStats.playerStats[0].Headshots.ShouldBe(1); // took over a bot for one of them
                allOutputData.AllStats.playerStats[0].Kills.ShouldBe(1); // took over a bot for one of them
                allOutputData.AllStats.playerStats[0].KillsIncludingBots.ShouldBe(2);
                allOutputData.AllStats.playerStats[0].MVPs.ShouldBe(2);
                allOutputData.AllStats.playerStats[0].Plants.ShouldBe(1);
                allOutputData.AllStats.playerStats[0].PlayerName.ShouldBe("JimWood");
                allOutputData.AllStats.playerStats[0].Rescues.ShouldBe(0);
                allOutputData.AllStats.playerStats[0].Shots.ShouldBe(6);
                allOutputData.AllStats.playerStats[0].SteamID.ShouldBe(32443298432);

                allOutputData.AllStats.playerStats[1].Assists.ShouldBe(1);
                allOutputData.AllStats.playerStats[1].AssistsIncludingBots.ShouldBe(1);
                allOutputData.AllStats.playerStats[1].Deaths.ShouldBe(1); // took over a bot for one of them
                allOutputData.AllStats.playerStats[1].DeathsIncludingBots.ShouldBe(2);
                allOutputData.AllStats.playerStats[1].Defuses.ShouldBe(0);
                allOutputData.AllStats.playerStats[1].Headshots.ShouldBe(0);
                allOutputData.AllStats.playerStats[1].Kills.ShouldBe(0);
                allOutputData.AllStats.playerStats[1].KillsIncludingBots.ShouldBe(0);
                allOutputData.AllStats.playerStats[1].MVPs.ShouldBe(0);
                allOutputData.AllStats.playerStats[1].Plants.ShouldBe(1);
                allOutputData.AllStats.playerStats[1].PlayerName.ShouldBe("TheWhaleMan");
                allOutputData.AllStats.playerStats[1].Rescues.ShouldBe(2);
                allOutputData.AllStats.playerStats[1].Shots.ShouldBe(1);
                allOutputData.AllStats.playerStats[1].SteamID.ShouldBe(12321313213);
            }

            [Fact]
            public void Should_return_rounds_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.roundsStats.Count.ShouldBe(2);

                allOutputData.AllStats.roundsStats[0].BombPlantPositionX.ShouldBe(100);
                allOutputData.AllStats.roundsStats[0].BombPlantPositionY.ShouldBe(100);
                allOutputData.AllStats.roundsStats[0].BombPlantPositionZ.ShouldBe(100);
                allOutputData.AllStats.roundsStats[0].BombsiteErrorMessage.ShouldBeNull();
                allOutputData.AllStats.roundsStats[0].BombsitePlantedAt.ShouldBe("A");
                allOutputData.AllStats.roundsStats[0].Half.ShouldBe("First");
                allOutputData.AllStats.roundsStats[0].HostageAPickedUpErrorMessage.ShouldBeNull();
                allOutputData.AllStats.roundsStats[0].HostageBPickedUpErrorMessage.ShouldBeNull();
                allOutputData.AllStats.roundsStats[0].Length.ShouldBe(80);
                allOutputData.AllStats.roundsStats[0].Overtime.ShouldBe(0);
                allOutputData.AllStats.roundsStats[0].PickedUpAllHostages.ShouldBe(true);
                allOutputData.AllStats.roundsStats[0].PickedUpHostageA.ShouldBe(true);
                allOutputData.AllStats.roundsStats[0].PickedUpHostageB.ShouldBe(true);
                allOutputData.AllStats.roundsStats[0].RescuedAllHostages.ShouldBe(true);
                allOutputData.AllStats.roundsStats[0].RescuedHostageA.ShouldBe(true);
                allOutputData.AllStats.roundsStats[0].RescuedHostageB.ShouldBe(true);
                allOutputData.AllStats.roundsStats[0].Round.ShouldBe(1);
                allOutputData.AllStats.roundsStats[0].TimeInRoundPlanted.ShouldBe(35);
                allOutputData.AllStats.roundsStats[0].TimeInRoundExploded.ShouldBe(75);
                allOutputData.AllStats.roundsStats[0].TimeInRoundDefused.ShouldBeNull();
                allOutputData.AllStats.roundsStats[0].TimeInRoundRescuedHostageA.ShouldBe(50);
                allOutputData.AllStats.roundsStats[0].TimeInRoundRescuedHostageB.ShouldBe(51);
                allOutputData.AllStats.roundsStats[0].WinMethod.ShouldBe("Bombed");
                allOutputData.AllStats.roundsStats[0].Winners.ShouldBe("Terrorist");

                allOutputData.AllStats.roundsStats[1].BombPlantPositionX.ShouldBe(400);
                allOutputData.AllStats.roundsStats[1].BombPlantPositionY.ShouldBe(400);
                allOutputData.AllStats.roundsStats[1].BombPlantPositionZ.ShouldBe(400);
                allOutputData.AllStats.roundsStats[1].BombsiteErrorMessage.ShouldBeNull();
                allOutputData.AllStats.roundsStats[1].BombsitePlantedAt.ShouldBe("B");
                allOutputData.AllStats.roundsStats[1].Half.ShouldBe("Second");
                allOutputData.AllStats.roundsStats[1].HostageAPickedUpErrorMessage.ShouldBeNull();
                allOutputData.AllStats.roundsStats[1].HostageBPickedUpErrorMessage.ShouldBeNull();
                allOutputData.AllStats.roundsStats[1].Length.ShouldBe(105);
                allOutputData.AllStats.roundsStats[1].Overtime.ShouldBe(0);
                allOutputData.AllStats.roundsStats[1].PickedUpAllHostages.ShouldBe(false);
                allOutputData.AllStats.roundsStats[1].PickedUpHostageA.ShouldBe(true);
                allOutputData.AllStats.roundsStats[1].PickedUpHostageB.ShouldBe(false);
                allOutputData.AllStats.roundsStats[1].RescuedAllHostages.ShouldBe(false);
                allOutputData.AllStats.roundsStats[1].RescuedHostageA.ShouldBe(false);
                allOutputData.AllStats.roundsStats[1].RescuedHostageB.ShouldBe(false);
                allOutputData.AllStats.roundsStats[1].Round.ShouldBe(2);
                allOutputData.AllStats.roundsStats[1].TimeInRoundPlanted.ShouldBe(60);
                allOutputData.AllStats.roundsStats[1].TimeInRoundExploded.ShouldBeNull();
                allOutputData.AllStats.roundsStats[1].TimeInRoundDefused.ShouldBe(100);
                allOutputData.AllStats.roundsStats[1].TimeInRoundRescuedHostageA.ShouldBeNull();
                allOutputData.AllStats.roundsStats[1].TimeInRoundRescuedHostageB.ShouldBeNull();
                allOutputData.AllStats.roundsStats[1].WinMethod.ShouldBe("Defused");
                allOutputData.AllStats.roundsStats[1].Winners.ShouldBe("CounterTerrorist");
            }

            [Fact]
            public void Should_return_supported_gamemodes_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.supportedGamemodes.Count.ShouldBe(5);
                allOutputData.AllStats.supportedGamemodes[0].ShouldBe(Gamemodes.Defuse);
                allOutputData.AllStats.supportedGamemodes[1].ShouldBe(Gamemodes.Hostage);
                allOutputData.AllStats.supportedGamemodes[2].ShouldBe(Gamemodes.WingmanDefuse);
                allOutputData.AllStats.supportedGamemodes[3].ShouldBe(Gamemodes.WingmanHostage);
                allOutputData.AllStats.supportedGamemodes[4].ShouldBe(Gamemodes.DangerZone);
            }

            [Fact]
            public void Should_return_tanooki_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.tanookiStats.Joined.ShouldBe(true);
                allOutputData.AllStats.tanookiStats.Left.ShouldBe(true);
                allOutputData.AllStats.tanookiStats.RoundJoined.ShouldBe(1);
                allOutputData.AllStats.tanookiStats.RoundLeft.ShouldBe(2);
                allOutputData.AllStats.tanookiStats.RoundsLasted.ShouldBe(1);
            }

            [Fact]
            public void Should_return_team_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.teamStats.Count.ShouldBe(2);

                allOutputData.AllStats.teamStats[0].Round.ShouldBe(1);
                allOutputData.AllStats.teamStats[0].TeamAlphaKills.ShouldBe(1);
                allOutputData.AllStats.teamStats[0].TeamAlphaDeaths.ShouldBe(0);
                allOutputData.AllStats.teamStats[0].TeamAlphaHeadshots.ShouldBe(1);
                allOutputData.AllStats.teamStats[0].TeamBravoKills.ShouldBe(0);
                allOutputData.AllStats.teamStats[0].TeamBravoDeaths.ShouldBe(1);
                allOutputData.AllStats.teamStats[0].TeamBravoHeadshots.ShouldBe(0);
                allOutputData.AllStats.teamStats[0].TeamAlphaShotsFired.ShouldBe(3);
                allOutputData.AllStats.teamStats[0].TeamBravoShotsFired.ShouldBe(0);

                allOutputData.AllStats.teamStats[1].Round.ShouldBe(2);
                allOutputData.AllStats.teamStats[1].TeamAlphaKills.ShouldBe(1);
                allOutputData.AllStats.teamStats[1].TeamAlphaDeaths.ShouldBe(0);
                allOutputData.AllStats.teamStats[1].TeamAlphaHeadshots.ShouldBe(1);
                allOutputData.AllStats.teamStats[1].TeamBravoKills.ShouldBe(0);
                allOutputData.AllStats.teamStats[1].TeamBravoDeaths.ShouldBe(1);
                allOutputData.AllStats.teamStats[1].TeamBravoHeadshots.ShouldBe(0);
                allOutputData.AllStats.teamStats[1].TeamAlphaShotsFired.ShouldBe(3);
                allOutputData.AllStats.teamStats[1].TeamBravoShotsFired.ShouldBe(1);
            }

            [Fact]
            public void Should_return_version_number_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.versionNumber.Version.ShouldBe(
                    Assembly.GetExecutingAssembly().GetName().Version.ToString(3)
                );
            }

            [Fact]
            public void Should_return_winners_stats_correctly()
            {
                // Arrange

                // Act
                AllOutputData allOutputData = MatchData.CreateFiles(ProcessedData, false);

                // Assess
                allOutputData.AllStats.winnersStats.TeamAlphaRounds.ShouldBe(2);
                allOutputData.AllStats.winnersStats.TeamBetaRounds.ShouldBe(0);
                allOutputData.AllStats.winnersStats.WinningTeam.ShouldBe("Team Alpha");
            }
        }
    }
}
