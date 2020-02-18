using DemoInfo;
using NSubstitute;
using Shouldly;
using System;
using System.Collections.Generic;
using TopStatsWaffle.Models;
using Xunit;

namespace TopStatsWaffle.Tests
{
	public class TopStatsWaffleTests
	{
		protected MatchData MatchData;
		protected ProcessedData ProcessedData;

		public TopStatsWaffleTests()
		{
			MatchData = new MatchData();
			MockData();

			foreach(var teamPlayers in ProcessedData.TeamPlayersValues)
			{
				foreach(var player in teamPlayers.Terrorists)
				{
					MatchData.BindPlayer(player);
				}
				foreach(var player in teamPlayers.CounterTerrorists)
				{
					MatchData.BindPlayer(player);
				}
			}
		}

		public class DataValidationTests : TopStatsWaffleTests
		{
			[Fact]
			public void Should_return_bombsite_stats_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.BombsiteStats.Count.ShouldBe(2);
				allStats.BombsiteStats[0].Plants.ShouldBe(1);
				allStats.BombsiteStats[0].Explosions.ShouldBe(1);
				allStats.BombsiteStats[0].Defuses.ShouldBe(0);
				allStats.BombsiteStats[1].Plants.ShouldBe(1);
				allStats.BombsiteStats[1].Explosions.ShouldBe(0);
				allStats.BombsiteStats[1].Defuses.ShouldBe(1);
			}

			[Fact]
			public void Should_return_chicken_stats_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.ChickenStats.Killed.ShouldBe(1);
			}

			[Fact]
			public void Should_return_feedback_messages_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.FeedbackMessages.Count.ShouldBe(1);
				allStats.FeedbackMessages[0].Round.ShouldBe(1);
				allStats.FeedbackMessages[0].Message.ShouldBe("bad map");
			}

			[Fact]
			public void Should_return_grenade_specific_stats_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.GrenadesSpecificStats.Count.ShouldBe(6);
				allStats.GrenadesSpecificStats[0].NadeType.ShouldBe(EquipmentElement.Flash.ToString());
				allStats.GrenadesSpecificStats[1].NadeType.ShouldBe(EquipmentElement.Smoke.ToString());
				allStats.GrenadesSpecificStats[2].NadeType.ShouldBe(EquipmentElement.HE.ToString());
				allStats.GrenadesSpecificStats[3].NadeType.ShouldBe(EquipmentElement.Molotov.ToString());
				allStats.GrenadesSpecificStats[4].NadeType.ShouldBe(EquipmentElement.Incendiary.ToString());
				allStats.GrenadesSpecificStats[5].NadeType.ShouldBe(EquipmentElement.Decoy.ToString());
			}

			[Fact]
			public void Should_return_grenade_total_stats_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.GrenadesTotalStats.Count.ShouldBe(5);
				allStats.GrenadesTotalStats[0].NadeType.ShouldBe(EquipmentElement.Flash.ToString());
				allStats.GrenadesTotalStats[0].AmountUsed.ShouldBe(1);
				allStats.GrenadesTotalStats[1].NadeType.ShouldBe(EquipmentElement.Smoke.ToString());
				allStats.GrenadesTotalStats[1].AmountUsed.ShouldBe(1);
				allStats.GrenadesTotalStats[2].NadeType.ShouldBe(EquipmentElement.HE.ToString());
				allStats.GrenadesTotalStats[2].AmountUsed.ShouldBe(1);
				allStats.GrenadesTotalStats[3].NadeType.ShouldBe(EquipmentElement.Incendiary.ToString());
				allStats.GrenadesTotalStats[3].AmountUsed.ShouldBe(2);
				allStats.GrenadesTotalStats[4].NadeType.ShouldBe(EquipmentElement.Decoy.ToString());
				allStats.GrenadesTotalStats[4].AmountUsed.ShouldBe(1);
			}

			[Fact]
			public void Should_return_hostage_stats_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.HostageStats.Count.ShouldBe(2);
				allStats.HostageStats[0].Hostage.ShouldBe('A');
				allStats.HostageStats[0].HostageIndex.ShouldBe(250);
				allStats.HostageStats[0].PickedUps.ShouldBe(2);
				allStats.HostageStats[0].Rescues.ShouldBe(1);
				allStats.HostageStats[1].Hostage.ShouldBe('B');
				allStats.HostageStats[1].HostageIndex.ShouldBe(251);
				allStats.HostageStats[1].PickedUps.ShouldBe(1);
				allStats.HostageStats[1].Rescues.ShouldBe(1);
			}

			[Fact]
			public void Should_return_kills_stats_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.KillsStats.Count.ShouldBe(2);
				allStats.KillsStats[0].Round.ShouldBe(1);
				allStats.KillsStats[0].TimeInRound.ShouldBe(40);
				allStats.KillsStats[1].Round.ShouldBe(2);
				allStats.KillsStats[1].TimeInRound.ShouldBe(90);
			}

			[Fact]
			public void Should_return_map_info_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.MapInfo.DemoName.ShouldBe("demo1");
				allStats.MapInfo.MapName.ShouldBe("de_testmap");
				allStats.MapInfo.TestDate.ShouldBe(new DateTime(2020, 1, 1, 0, 0, 0).ToString());
				allStats.MapInfo.TestType.ShouldBe("Defuse");
			}

			[Fact]
			public void Should_return_player_stats_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.PlayerStats.Count.ShouldBe(2);

				allStats.PlayerStats[0].Assists.ShouldBe(0);
				allStats.PlayerStats[0].AssistsIncludingBots.ShouldBe(0);
				allStats.PlayerStats[0].Deaths.ShouldBe(0);
				allStats.PlayerStats[0].DeathsIncludingBots.ShouldBe(0);
				allStats.PlayerStats[0].Defuses.ShouldBe(1);
				allStats.PlayerStats[0].Headshots.ShouldBe(1); // took over a bot for one of them
				allStats.PlayerStats[0].Kills.ShouldBe(1); // took over a bot for one of them
				allStats.PlayerStats[0].KillsIncludingBots.ShouldBe(2);
				allStats.PlayerStats[0].MVPs.ShouldBe(2);
				allStats.PlayerStats[0].Plants.ShouldBe(1);
				allStats.PlayerStats[0].PlayerName.ShouldBe("JimWood");
				allStats.PlayerStats[0].Rescues.ShouldBe(0);
				allStats.PlayerStats[0].Shots.ShouldBe(6);
				allStats.PlayerStats[0].SteamID.ShouldBe(32443298432);

				allStats.PlayerStats[1].Assists.ShouldBe(1);
				allStats.PlayerStats[1].AssistsIncludingBots.ShouldBe(1);
				allStats.PlayerStats[1].Deaths.ShouldBe(1); // took over a bot for one of them
				allStats.PlayerStats[1].DeathsIncludingBots.ShouldBe(2);
				allStats.PlayerStats[1].Defuses.ShouldBe(0);
				allStats.PlayerStats[1].Headshots.ShouldBe(0);
				allStats.PlayerStats[1].Kills.ShouldBe(0);
				allStats.PlayerStats[1].KillsIncludingBots.ShouldBe(0);
				allStats.PlayerStats[1].MVPs.ShouldBe(0);
				allStats.PlayerStats[1].Plants.ShouldBe(1);
				allStats.PlayerStats[1].PlayerName.ShouldBe("TheWhaleMan");
				allStats.PlayerStats[1].Rescues.ShouldBe(2);
				allStats.PlayerStats[1].Shots.ShouldBe(1);
				allStats.PlayerStats[1].SteamID.ShouldBe(12321313213);
			}

			[Fact]
			public void Should_return_rounds_stats_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.RoundsStats.Count.ShouldBe(2);

				allStats.RoundsStats[0].BombPlantPositionX.ShouldBe(100);
				allStats.RoundsStats[0].BombPlantPositionY.ShouldBe(100);
				allStats.RoundsStats[0].BombPlantPositionZ.ShouldBe(100);
				allStats.RoundsStats[0].BombsiteErrorMessage.ShouldBeNull();
				allStats.RoundsStats[0].BombsitePlantedAt.ShouldBe("A");
				allStats.RoundsStats[0].Half.ShouldBe("First");
				allStats.RoundsStats[0].HostageAPickedUpErrorMessage.ShouldBeNull();
				allStats.RoundsStats[0].HostageBPickedUpErrorMessage.ShouldBeNull();
				allStats.RoundsStats[0].Length.ShouldBe(80);
				allStats.RoundsStats[0].Overtime.ShouldBe(0);
				allStats.RoundsStats[0].PickedUpAllHostages.ShouldBe(true);
				allStats.RoundsStats[0].PickedUpHostageA.ShouldBe(true);
				allStats.RoundsStats[0].PickedUpHostageB.ShouldBe(true);
				allStats.RoundsStats[0].RescuedAllHostages.ShouldBe(true);
				allStats.RoundsStats[0].RescuedHostageA.ShouldBe(true);
				allStats.RoundsStats[0].RescuedHostageB.ShouldBe(true);
				allStats.RoundsStats[0].Round.ShouldBe(1);
				allStats.RoundsStats[0].TimeInRoundPlanted.ShouldBe(35);
				allStats.RoundsStats[0].TimeInRoundExploded.ShouldBe(75);
				allStats.RoundsStats[0].TimeInRoundDefused.ShouldBeNull();
				allStats.RoundsStats[0].TimeInRoundRescuedHostageA.ShouldBe(50);
				allStats.RoundsStats[0].TimeInRoundRescuedHostageB.ShouldBe(51);
				allStats.RoundsStats[0].WinMethod.ShouldBe("Bombed");
				allStats.RoundsStats[0].Winners.ShouldBe("Terrorist");

				allStats.RoundsStats[1].BombPlantPositionX.ShouldBe(400);
				allStats.RoundsStats[1].BombPlantPositionY.ShouldBe(400);
				allStats.RoundsStats[1].BombPlantPositionZ.ShouldBe(400);
				allStats.RoundsStats[1].BombsiteErrorMessage.ShouldBeNull();
				allStats.RoundsStats[1].BombsitePlantedAt.ShouldBe("B");
				allStats.RoundsStats[1].Half.ShouldBe("Second");
				allStats.RoundsStats[1].HostageAPickedUpErrorMessage.ShouldBeNull();
				allStats.RoundsStats[1].HostageBPickedUpErrorMessage.ShouldBeNull();
				allStats.RoundsStats[1].Length.ShouldBe(105);
				allStats.RoundsStats[1].Overtime.ShouldBe(0);
				allStats.RoundsStats[1].PickedUpAllHostages.ShouldBe(false);
				allStats.RoundsStats[1].PickedUpHostageA.ShouldBe(true);
				allStats.RoundsStats[1].PickedUpHostageB.ShouldBe(false);
				allStats.RoundsStats[1].RescuedAllHostages.ShouldBe(false);
				allStats.RoundsStats[1].RescuedHostageA.ShouldBe(false);
				allStats.RoundsStats[1].RescuedHostageB.ShouldBe(false);
				allStats.RoundsStats[1].Round.ShouldBe(2);
				allStats.RoundsStats[1].TimeInRoundPlanted.ShouldBe(60);
				allStats.RoundsStats[1].TimeInRoundExploded.ShouldBeNull();
				allStats.RoundsStats[1].TimeInRoundDefused.ShouldBe(100);
				allStats.RoundsStats[1].TimeInRoundRescuedHostageA.ShouldBeNull();
				allStats.RoundsStats[1].TimeInRoundRescuedHostageB.ShouldBeNull();
				allStats.RoundsStats[1].WinMethod.ShouldBe("Defused");
				allStats.RoundsStats[1].Winners.ShouldBe("CounterTerrorist");
			}

			[Fact]
			public void Should_return_supported_gamemodes_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.SupportedGamemodes.Count.ShouldBe(3);
				allStats.SupportedGamemodes[0].ShouldBe("Defuse");
				allStats.SupportedGamemodes[1].ShouldBe("Hostage");
				allStats.SupportedGamemodes[2].ShouldBe("Wingman");
			}

			[Fact]
			public void Should_return_tanooki_stats_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.TanookiStats.Joined.ShouldBe(true);
				allStats.TanookiStats.Left.ShouldBe(true);
				allStats.TanookiStats.RoundJoined.ShouldBe(1);
				allStats.TanookiStats.RoundLeft.ShouldBe(2);
				allStats.TanookiStats.RoundsLasted.ShouldBe(1);
			}

			[Fact]
			public void Should_return_team_stats_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.TeamStats.Count.ShouldBe(2);

				allStats.TeamStats[0].Round.ShouldBe(1);
				allStats.TeamStats[0].TeamAlphaKills.ShouldBe(1);
				allStats.TeamStats[0].TeamAlphaDeaths.ShouldBe(0);
				allStats.TeamStats[0].TeamAlphaHeadshots.ShouldBe(1);
				allStats.TeamStats[0].TeamBravoKills.ShouldBe(0);
				allStats.TeamStats[0].TeamBravoDeaths.ShouldBe(1);
				allStats.TeamStats[0].TeamBravoHeadshots.ShouldBe(0);
				allStats.TeamStats[0].TeamAlphaShotsFired.ShouldBe(3);
				allStats.TeamStats[0].TeamBravoShotsFired.ShouldBe(0);

				allStats.TeamStats[1].Round.ShouldBe(2);
				allStats.TeamStats[1].TeamAlphaKills.ShouldBe(1);
				allStats.TeamStats[1].TeamAlphaDeaths.ShouldBe(0);
				allStats.TeamStats[1].TeamAlphaHeadshots.ShouldBe(1);
				allStats.TeamStats[1].TeamBravoKills.ShouldBe(0);
				allStats.TeamStats[1].TeamBravoDeaths.ShouldBe(1);
				allStats.TeamStats[1].TeamBravoHeadshots.ShouldBe(0);
				allStats.TeamStats[1].TeamAlphaShotsFired.ShouldBe(3);
				allStats.TeamStats[1].TeamBravoShotsFired.ShouldBe(1);
			}

			[Fact]
			public void Should_return_version_number_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.VersionNumber.Version.ShouldBe("1.1.9");
			}

			[Fact]
			public void Should_return_winners_stats_correctly()
			{
				// Arrange

				// Act
				AllStats allStats = MatchData.CreateFiles(ProcessedData, false);

				// Assess
				allStats.WinnersStats.TeamAlphaRounds.ShouldBe(2);
				allStats.WinnersStats.TeamBetaRounds.ShouldBe(0);
				allStats.WinnersStats.WinningTeam.ShouldBe("Team Alpha");
			}
		}

		public void MockData()
		{
			var DemoInformation = new DemoInformation()
			{
				DemoName = "demo1",
				MapName = "de_testmap",
				TestDate = new DateTime(2020, 1, 1, 0, 0, 0).ToString(),
				TestType = "Defuse",
			};

			var TanookiStats = new TanookiStats()
			{
				Joined = true,
				Left = true,
				RoundJoined = 1,
				RoundLeft = 2,
				RoundsLasted = 1,
			};

			var MatchStartValues = new List<MatchStartedEventArgs>()
			{
				new MatchStartedEventArgs
				{
					Mapname = "de_testmap",
					HasBombsites = true,
				}
			};

			var SwitchSidesValues = new List<SwitchSidesEventArgs>()
			{
				new SwitchSidesEventArgs
				{
					RoundBeforeSwitch = 1,
				}
			};

			var MessagesValues = new List<FeedbackMessage>()
			{
				new FeedbackMessage()
				{
					Round = 1,
					SteamID = 32443298432,
					TeamName = "AlphaTeam",
					Message = "bad map",
				}
			};

			var TeamPlayersValues = new List<TeamPlayers>()
			{
				new TeamPlayers()
				{
					Round = 1,
					Terrorists = new List<Player>()
					{
						new Player
						{
							Name = "JimWood",
							SteamID = 32443298432,
							EntityID = 45,
							UserID = 1,
							LastAlivePosition = new Vector()
							{
								X = 100,
								Y = 100,
								Z = 100,
							},
							Position = new Vector()
							{
								X = 200,
								Y = 200,
								Z = 200,
							},
							Money = 200,
							RoundStartEquipmentValue = 2700,
						}
					},
					CounterTerrorists = new List<Player>()
					{
						new Player
						{
							Name = "TheWhaleMan",
							SteamID = 12321313213,
							EntityID = 46,
							UserID = 2,
							LastAlivePosition = new Vector()
							{
								X = 90,
								Y = 900,
								Z = 9000,
							},
							Position = new Vector()
							{
								X = 80,
								Y = 800,
								Z = 8000,
							},
							Money = 200,
							RoundStartEquipmentValue = 200,
						}
					}
				},
				new TeamPlayers()
				{
					Round = 2,
					Terrorists = new List<Player>()
					{
						new Player
						{
							Name = "TheWhaleMan",
							SteamID = 12321313213,
							EntityID = 46,
							UserID = 2,
							LastAlivePosition = new Vector()
							{
								X = 400,
								Y = 400,
								Z = 400,
							},
							Position = new Vector()
							{
								X = 500,
								Y = 500,
								Z = 500,
							},
							Money = 1000,
							RoundStartEquipmentValue = 200,
						}
					},
					CounterTerrorists = new List<Player>()
					{
						new Player
						{
							Name = "JimWood",
							SteamID = 32443298432,
							EntityID = 45,
							UserID = 1,
							LastAlivePosition = new Vector()
							{
								X = 70,
								Y = 70,
								Z = 70,
							},
							Position = new Vector()
							{
								X = 60,
								Y = 60,
								Z = 60,
							},
							Money = 5000,
							RoundStartEquipmentValue = 4750,
						}
					}
				}
			};

			var PlayerKilledEventsValues = new List<PlayerKilledEventArgs>()
			{
				new PlayerKilledEventArgs
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
				new PlayerKilledEventArgs
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
				}
			};

			var PlayerValues = new Dictionary<string, IEnumerable<Player>>()
			{
				{
					"Kills",
					new List<Player>()
					{
						TeamPlayersValues[0].Terrorists[0],
						TeamPlayersValues[1].CounterTerrorists[0],
					}
				},
				{
					"Deaths",
					new List<Player>()
					{
						TeamPlayersValues[0].CounterTerrorists[0],
						TeamPlayersValues[1].Terrorists[0],
					}
				},
				{
					"Headshots",
					new List<Player>()
					{
						TeamPlayersValues[0].Terrorists[0],
					}
				},
				{
					"Assists",
					new List<Player>()
					{
						TeamPlayersValues[0].CounterTerrorists[0],
					}
				},
				{
					"MVPs",
					new List<Player>()
					{
						TeamPlayersValues[0].Terrorists[0],
						TeamPlayersValues[1].CounterTerrorists[0],
					}
				},
				{
					"Shots",
					new List<Player>()
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
					"Plants",
					new List<Player>()
					{
						TeamPlayersValues[0].Terrorists[0],
						TeamPlayersValues[1].Terrorists[0],
					}
				},
				{
					"Defuses",
					new List<Player>()
					{
						TeamPlayersValues[1].CounterTerrorists[0],
					}
				},
				{
					"Rescues",
					new List<Player>()
					{
						TeamPlayersValues[0].CounterTerrorists[0],
						TeamPlayersValues[0].CounterTerrorists[0],
					}
				}
			};

			var WeaponValues = new List<Equipment>()
			{
				new Equipment
				{
					Owner = TeamPlayersValues[0].Terrorists[0],
					Weapon = EquipmentElement.AK47
				},
				new Equipment
				{
					Owner = TeamPlayersValues[0].CounterTerrorists[0],
					Weapon = EquipmentElement.AWP
				}
			};

			var PenetrationValues = new List<int>()
			{
				0,
				1,
			};

			var BombsitePlantValues = new List<BombPlanted>()
			{
				new BombPlanted
				{
					Bombsite = 'A',
					Player = TeamPlayersValues[0].Terrorists[0],
					Round = 1,
					TimeInRound = 35,
					XPosition = 100,
					YPosition = 100,
					ZPosition = 100,
				},
				new BombPlanted
				{
					Bombsite = 'B',
					Player = TeamPlayersValues[1].Terrorists[0],
					Round = 2,
					TimeInRound = 60,
					XPosition = 400,
					YPosition = 400,
					ZPosition = 400,
				}
			};

			var BombsiteExplodeValues = new List<BombExploded>()
			{
				new BombExploded
				{
					Bombsite = 'A',
					Player = TeamPlayersValues[0].Terrorists[0],
					Round = 1,
					TimeInRound = 75,
				}
			};

			var BombsiteDefuseValues = new List<BombDefused>()
			{
				new BombDefused
				{
					Bombsite = 'B',
					Player = TeamPlayersValues[1].CounterTerrorists[0],
					Round = 2,
					TimeInRound = 100,
					HasKit = true,
				}
			};

			var HostageRescueValues = new List<HostageRescued>()
			{
				new HostageRescued
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
				new HostageRescued
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
				}
			};

			var HostagePickedUpValues = new List<HostagePickedUp>()
			{
				new HostagePickedUp
				{
					Hostage = 'A',
					HostageIndex = 250,
					Player = TeamPlayersValues[0].CounterTerrorists[0],
					Round = 1,
					TimeInRound = 20,
				},
				new HostagePickedUp
				{
					Hostage = 'B',
					HostageIndex = 251,
					Player = TeamPlayersValues[0].CounterTerrorists[0],
					Round = 1,
					TimeInRound = 35,
				},
				new HostagePickedUp
				{
					Hostage = 'A',
					HostageIndex = 250,
					Player = TeamPlayersValues[1].CounterTerrorists[0],
					Round = 2,
					TimeInRound = 40,
				}
			};

			var TeamValues = new List<Team>()
			{
				Team.Terrorist,
				Team.CounterTerrorist,
			};

			var RoundEndReasonValues = new List<RoundEndReason>()
			{
				RoundEndReason.TargetBombed,
				RoundEndReason.BombDefused,
			};

			var RoundLengthValues = new List<double>()
			{
				80,
				105,
			};

			var TeamEquipmentValues = new List<TeamEquipmentStats>()
			{
				new TeamEquipmentStats
				{
					Round = 1,
					TEquipValue = 2900,
					TExpenditure = 200,
					CTEquipValue = 450,
					CTExpenditure = 50,
				},
				new TeamEquipmentStats
				{
					Round = 2,
					TEquipValue = 800,
					TExpenditure = 600,
					CTEquipValue = 5750,
					CTExpenditure = 1000,
				}
			};

			var GrenadeValues = new List<NadeEventArgs>()
			{
				new FlashEventArgs
				{
					NadeType = EquipmentElement.Flash,
					ThrownBy = TeamPlayersValues[0].Terrorists[0],
					Position = new Vector()
					{
						X = 500,
						Y = 500,
						Z = 500,
					},
					FlashedPlayers = new Player[1]
					{
						TeamPlayersValues[0].CounterTerrorists[0]
					}
				} as NadeEventArgs,
				new NadeEventArgs
				{
					NadeType = EquipmentElement.Smoke,
					ThrownBy = TeamPlayersValues[0].Terrorists[0],
					Position = new Vector()
					{
						X = 500,
						Y = 500,
						Z = 500,
					},
				},
				new NadeEventArgs
				{
					NadeType = EquipmentElement.HE,
					ThrownBy = TeamPlayersValues[0].Terrorists[0],
					Position = new Vector()
					{
						X = 500,
						Y = 500,
						Z = 500,
					},
				},
				new NadeEventArgs
				{
					NadeType = EquipmentElement.Molotov, // all molotovs are down as incendiaries, specified why in DemoParser.cs, search for "FireNadeStarted".
					ThrownBy = TeamPlayersValues[0].Terrorists[0],
					Position = new Vector()
					{
						X = 500,
						Y = 500,
						Z = 500,
					},
				},
				new NadeEventArgs
				{
					NadeType = EquipmentElement.Incendiary,
					ThrownBy = TeamPlayersValues[0].Terrorists[0],
					Position = new Vector()
					{
						X = 500,
						Y = 500,
						Z = 500,
					},
				},
				new NadeEventArgs
				{
					NadeType = EquipmentElement.Decoy,
					ThrownBy = TeamPlayersValues[0].Terrorists[0],
					Position = new Vector()
					{
						X = 500,
						Y = 500,
						Z = 500,
					},
				}
			};

			var ChickenValues = new List<ChickenKilledEventArgs>()
			{
				new ChickenKilledEventArgs {}
			};

			var ShotsFiredValues = new List<ShotFired>()
			{
				new ShotFired
				{
					Round = 1,
					Shooter = TeamPlayersValues[0].Terrorists[0]
				},
				new ShotFired
				{
					Round = 1,
					Shooter = TeamPlayersValues[0].Terrorists[0]
				},
				new ShotFired
				{
					Round = 1,
					Shooter = TeamPlayersValues[0].Terrorists[0]
				},
				new ShotFired
				{
					Round = 2,
					Shooter = TeamPlayersValues[1].Terrorists[0]
				},
				new ShotFired
				{
					Round = 2,
					Shooter = TeamPlayersValues[1].CounterTerrorists[0]
				},
				new ShotFired
				{
					Round = 2,
					Shooter = TeamPlayersValues[1].CounterTerrorists[0]
				},
				new ShotFired
				{
					Round = 2,
					Shooter = TeamPlayersValues[1].CounterTerrorists[0]
				}
			};


			ProcessedData = new ProcessedData()
			{
				DemoInformation = DemoInformation,
				SameFilename = true,
				SameFolderStructure = true,
				ParseChickens = true,
				FoldersToProcess = new List<string>() { "someFolder" },
				OutputRootFolder = "outputFolder",
				TanookiStats = TanookiStats,
				MatchStartValues = MatchStartValues,
				SwitchSidesValues = SwitchSidesValues,
				MessagesValues = MessagesValues,
				TeamPlayersValues = TeamPlayersValues,
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
				WriteTicks = true
			};
		}
	}
}
