# CSGODemoCSV
Based on DemoInfoGO

Latest compiled version 2.0.1 Download: http://harrygodden.com/downloads/CSGODemoCSV2.0.1.zip

![Program output](https://i.imgur.com/RALmTAR.png)

## Usage

Example: `CSGODemoCSV -folders "C:/Folder1/" "/relativeFolder/" -recursive -concat`
```
  -folders    [paths (space seperated)]  Processes all demo files in each folder specified
  -demos      [paths (space seperated)]  Processess a list of single demo files at paths
  -recursive                             Switch for recursive demo search
  -noguid                                Disables GUID prefix
  -concat                                Joins everthing into one big csv. Also makes use of -noguid
```

## Modifying
CSGODemoCSV now uses a database oriented system

You can add your own queries using LINQ to `Program.cs` around line 150

### Example
```CSharp
//Used to store the events that get written to the CSV file
Dictionary<string, IEnumerable<Player>> cEvents = new Dictionary<string, IEnumerable<Player>>();

cEvents.Add("Assists", from player in mdTest.getEvents<PlayerKilledEventArgs>() //Grab the PlayerKilled table
                    where (player as PlayerKilledEventArgs).Assister != null    //If the kill-assister is present
                    select (player as PlayerKilledEventArgs).Assister);         //Select that assister for the list
                    
//Outputs the CSV file
mdTest.SaveCSV("filepath.csv", cEvents);
```
### Availible events in 2.0.0
- PlayerKilledEventArgs
- BombEventArgs
- WeaponFiredEventArgs
- GrenadeEventArgs
- FireEventArgs
- SmokeEventArgs
- FlashEventArgs
