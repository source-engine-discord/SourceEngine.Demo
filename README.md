# SourceEngine.Demo

Outputs game statistics from CS:GO demos as JSON. Extended from CSGODemoCSV. Utilises SourceEngine.Demo.Parser, a library which extends [DemoInfo][9]. Available as both a class library on nuget.org and a CLI binary (named _IDemO_).

![Program output](https://i.imgur.com/RALmTAR.png)

#### Supported Gamemodes

- Defuse
- Hostage
- Wingman

## Running IDemO

### Releases

Only builds/releases for Windows are currently automated.

Latest release:

* [Windows (x64)][1]

### Usage

```
  -config               [path]                      Path to config file.
  -folders              [paths (space seperated)]   Processes all demo files in each folder specified.
  -demos                [paths (space seperated)]   Processess a list of single demo files at paths.
  -recursive                                        Switch for recursive demo search.
  -steaminfo                                        Takes steam names from steam.
  -clear                                            Clears the data folder.
  -nochickens                                       Disables checks for number of chickens killed when parsing.
  -samefilename                                     Uses the demo's filename as the output filename.
  -samefolderstructure                              Uses the demo's folder structure inside the root folder for the output json file.
```

Example:

```
IDemO -folders "demos" -output "parsed" -recursive -nochickens -samefilename -samefolderstructure
```

## Development

### Requirements

* [.NET Core 3.1 SDK][2] or higher.
* [Visual Studio 2019][3], [JetBrains Rider][4], or [Visual Studio Code][5] recommended.

### Building

The `SourceEngine.Demo.Stats.App` project will build the executable. By default, a [self-contained][6] [single executable][7] is published targeting `win-x64`.

#### Visual Studio

Right-click the `SourceEngine.Demo.Stats.App` project and click `Publish`. A new tab will open and it should have the `win_x64_self_contained` profile already selected. Take note of the target location shown (i.e. where the resulting exe will be) and then click the `Publish` button.

#### Command Line

Run

```
dotnet publish -c Release
```

By default, this publishes for the `win-x64` runtime. To build for another runtime, specify the [RID][8] with the `-r` option. For example:

```
dotnet publish -c Release -r linux-x64
```

[1]: https://github.com/source-engine-discord/SourceEngine.Demo/releases/latest/download/IDemO_win-x64.zip
[2]: https://dotnet.microsoft.com/download/dotnet-core/3.1
[3]: https://visualstudio.microsoft.com/
[4]: https://www.jetbrains.com/rider/
[5]: https://code.visualstudio.com/
[6]: https://docs.microsoft.com/en-us/dotnet/core/deploying/index#self-contained-deployments-scd
[7]: https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-core-3-0#single-file-executables
[8]: https://docs.microsoft.com/en-us/dotnet/core/rid-catalog
[9]: https://github.com/StatsHelix/demoinfo/
