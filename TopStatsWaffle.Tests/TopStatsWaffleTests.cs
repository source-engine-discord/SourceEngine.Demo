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
		protected readonly MatchData MatchData;

		public TopStatsWaffleTests()
		{
			MatchData = Substitute.For<MatchData>();
			MockData();
		}

		public class DataValidationTests : TopStatsWaffleTests
		{
			[Fact]
			public void Should_return_green_when_the_student_has_no_missed_lectures_or_courseworks()
			{
				// Arrange

				// Act
				//var zzzzzzzzzzzzz = MatchData.FromDemoFile();
				var json = MatchData.CreateFiles();

				// Assess
				json["somethingggggggggg"].ShouldBe("green");
			}
		}

		public void MockData()
		{
			var DemoInformation = new List<DemoInformation>()
			{
				new DemoInformation()
				{
					DemoName = "demo1",
					MapName = "de_testmap",
					TestDate = DateTime.Now.ToString(),
					TestType = "Defuse",
				}
			};

			var TanookiStats = new List<TanookiStats>()
			{
				new TanookiStats()
				{
					Joined = true,
					Left = true,
					RoundJoined = 1,
					RoundLeft = 2,
					RoundsLasted = 1,
				}
			};

			var MatchStartValues = new List<MatchStartedEventArgs>()
			{
				new MatchStartedEventArgs()
				{
					Mapname = "de_testmap",
					HasBombsites = true,
				}
			};

			var SwitchSidesValues = new List<SwitchSidesEventArgs>()
			{
				new SwitchSidesEventArgs()
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

			var DisconnectedPlayers = new List<DisconnectedPlayer>()
			{
				new DisconnectedPlayer
				{
					PlayerDisconnectEventArgs = new PlayerDisconnectEventArgs()
					{
						Player = new Player()
						{
							Name = "TanookiSuit3",
							EntityID = 69,
							Disconnected = true,
						}
					}
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

			};

			var ChickenKilledEventArgs = new List<ChickenKilledEventArgs>()
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
					Round = 2,
					Shooter = TeamPlayersValues[0].CounterTerrorists[0]
				}
			};




			/*
				DemoInformation = demosInformation[i],
				SameFilename = sameFilename,
				SameFolderStructure = sameFolderStructure,
				ParseChickens = parseChickens,
				FoldersToProcess = foldersToProcess,
				OutputRootFolder = outputRootFolder,
				TanookiStats = tanookiStats,
				MatchStartValues = mse,
				SwitchSidesValues = sse,
				MessagesValues = fme,
				TeamPlayersValues = tpe,
				PlayerKilledEventsValues = pke,
				PlayerValues = pe,
				WeaponValues = pwe,
				PenetrationValues = poe,
				BombsitePlantValues = bpe,
				BombsiteExplodeValues = bee,
				BombsiteDefuseValues = bde,
				BombsiteValues = be,
				HostageRescueValues = hre,
				HostagePickedUpValues = hpu,
				HostageValues = he,
				TeamValues = te,
				RoundEndReasonValues = re,
				RoundLengthValues = le,
				TeamEquipmentValues = tes,
				GrenadeValues = ge,
				ChickenValues = cke,
				ShotsFiredValues = sfe,
			*/



			/*
			var AllStats = new List<AllStats>
			{
				new AllStats
				{
					VersionNumber = new VersionNumber {
						Version = "0.0.0",
					},
					SupportedGamemodes,
					MapInfo,
					TanookiStats,
					PlayerStats,
					WinnersStats,
					RoundsStats,
					BombsiteStats,
					HostageStats,
					GrenadesTotalStats,
					GrenadesSpecificStats,
					KillsStats,
					FeedbackMessages,
					ChickenStats,
					TeamStats,
				}
			};
			*/
		}
	}
}
