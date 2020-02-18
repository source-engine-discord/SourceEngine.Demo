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
				allStats.HostageStats[0].PickedUps.ShouldBe(1);
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
					RoundBeforeSwitch = 15,
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
							LastAlivePosition = new Vector()
							{
								X = 10,
								Y = 100,
								Z = 1000,
							},
							Position = new Vector()
							{
								X = 20,
								Y = 200,
								Z = 2000,
							},
							Money = 4000,
							RoundStartEquipmentValue = 500,
						}
					},
					CounterTerrorists = new List<Player>()
					{
						new Player
						{
							Name = "TheWhaleMan",
							SteamID = 12321313213,
							EntityID = 46,
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
							Money = 50,
							RoundStartEquipmentValue = 100,
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
				},
				new PlayerKilledEventArgs
				{
					Round = 2,
					TimeInRound = 90,
				}
			};

			var PlayerValues = new Dictionary<string, IEnumerable<Player>>()
			{
				{
					"Kills",
					new List<Player>()
					{
						TeamPlayersValues[0].Terrorists[0],
						TeamPlayersValues[0].CounterTerrorists[0],
					}
				},
				{
					"Deaths",
					new List<Player>()
					{
						TeamPlayersValues[0].CounterTerrorists[0],
						TeamPlayersValues[0].Terrorists[0],
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
						TeamPlayersValues[0].CounterTerrorists[0],
					}
				},
				{
					"Shots",
					new List<Player>()
					{
						TeamPlayersValues[0].Terrorists[0],
						TeamPlayersValues[0].Terrorists[0],
						TeamPlayersValues[0].Terrorists[0],
						TeamPlayersValues[0].CounterTerrorists[0],
						TeamPlayersValues[0].CounterTerrorists[0],
						TeamPlayersValues[0].CounterTerrorists[0],
					}
				},
				{
					"Plants",
					new List<Player>()
					{
						TeamPlayersValues[0].Terrorists[0],
					}
				},
				{
					"Defuses",
					new List<Player>()
					{
						TeamPlayersValues[0].CounterTerrorists[0],
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
					XPosition = 50,
					YPosition = 50,
					ZPosition = 50,
				},
				new BombPlanted
				{
					Bombsite = 'B',
					Player = TeamPlayersValues[0].Terrorists[0],
					Round = 2,
					TimeInRound = 60,
					XPosition = 100,
					YPosition = 100,
					ZPosition = 100,
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
					Player = TeamPlayersValues[0].Terrorists[0],
					Round = 1,
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
					TimeInRound = 50,
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
				}
			};

			var TeamValues = new List<Team>()
			{

			};

			var RoundEndReasonValues = new List<RoundEndReason>()
			{

			};

			var RoundLengthValues = new List<double>()
			{

			};

			var TeamEquipmentValues = new List<TeamEquipmentStats>()
			{

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
					Shooter = TeamPlayersValues[0].CounterTerrorists[0]
				},
				new ShotFired
				{
					Round = 2,
					Shooter = TeamPlayersValues[0].CounterTerrorists[0]
				},
				new ShotFired
				{
					Round = 2,
					Shooter = TeamPlayersValues[0].CounterTerrorists[0]
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
