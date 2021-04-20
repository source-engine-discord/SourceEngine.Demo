# SourceEngine.Demo

Outputs game statistics from CS:GO demos as JSON. The solution has three projects:

**SourceEngine.Demo.Parser** - A library used to parse CS:GO demos. It is based on [DemoInfo][9].

**SourceEngine.Demo.Stats** - A library for gathering statistics from demos and outputting them as JSON. It is based on [CSGODemoCSV][12] and is available as a package on nuget.org.

**SourceEngine.Demo.Stats.App** - A .NET Core app which provides a command line interface for SourceEngine.Demo.Stats. Also known as _IDemO_.

![Program output](https://user-images.githubusercontent.com/1515135/115470721-9985c780-a1eb-11eb-8bb9-f2351c019c3c.png)

#### Supported Gamemodes

- Defuse
- Hostage
- Wingman Defuse
- Wingman Hostage
- Danger Zone

## Running IDemO

### Releases

Only builds/releases for Windows are currently automated.

Latest release:

* [Windows (x64)][1]

### Usage

```
  --folders                           Space-delimited list of directories in
                                      which to search for demos to parse.

  --demos                             Space-delimited list of paths to
                                      individual demos to parse.

  --output                            (Default: parsed) Path to the output
                                      directory.

  --gamemodeoverride                  (Default: Unknown) Assume the demo is for
                                      this game mode rather than attempting to
                                      infer the game mode. Valid values:
                                      DangerZone, Defuse, Hostage,
                                      WingmanDefuse, WingmanHostage, Unknown

  --testtype                          (Default: Unknown) The playtest type of
                                      the recorded match. If unset, attempt to
                                      parse it from the file name instead
                                      assuming the format
                                      date_mapname_testtype.Only relevant for
                                      defuse and hostage game modes. Valid
                                      values: Casual, Competitive, Unknown

  --testdateoverride                  Recording date of the match in dd/MM/yyyy
                                      format. If unset, attempt to parse from
                                      the file name instead assuming the format
                                      date_mapname_testtype.

  --hostagerescuezonecountoverride    Number of hostage rescue zones in the map.
                                      If unset, a total of 1 is assumed for the
                                      hostage game mode and 2 for Danger Zone.
                                      Valid values: 0-2

  --recursive                         (Default: false) Recursively search for
                                      demos.

  --clear                             (Default: false) Clear the data folder.

  --nochickens                        (Default: false) Disable counting of
                                      chicken death stats.

  --noplayerpositions                 (Default: false) Disable parsing of player
                                      positions.

  --samefilename                      (Default: false) Use the demo's filename
                                      as the output filename.

  --samefolderstructure               (Default: false) Use the demo's folder
                                      structure inside the root folder for the
                                      output JSON file.

  --lowoutputmode                     (Default: false) Don't output the progress
                                      bar and 'round completed' messages to the
                                      console.

  --help                              Display this help screen.

  --version                           Display version information.
```

Example:

```
IDemO --folders "demos" --output "parsed" --recursive --nochickens --noplayerpositions --samefilename --samefolderstructure
```

## Development

### Requirements

* [.NET Core 5.0 SDK][2]
* [Visual Studio 2019][3], [JetBrains Rider][4], or [Visual Studio Code][5] recommended.

### Building

The `SourceEngine.Demo.Stats.App` project will build the executable. By default, a [self-contained][6] [single executable][7] is published targeting `win-x64`.

#### Visual Studio

Build as one normally would. To publish, right-click the `SourceEngine.Demo.Stats.App` project and click `Publish`. A new tab will open and it should have the `win_x64_self_contained` profile already selected. Take note of the target location shown (i.e. where the resulting exe will be) and then click the `Publish` button.

#### Command Line

To build and publish a release, run

```
dotnet publish -c Release
```

By default, this publishes for the `win-x64` runtime. To build for another runtime, specify the [RID][8] with the `-r` option. For example:

```
dotnet publish -c Release -r linux-x64
```

### Creating a Release

Update the version number in [`Directory.Build.props`][10] and commit the change. Then, create a tag with git. It is recommended to use annotated tags:

```
git tag -a v3.5.0 -m 'A brief description of the release'
```

Note that CI enforces [SemVer 2.0.0][11] format compliance as well as the versions in the tag and the project being equal.

Finally, push the tag along with the commit for the version bump:

```
git push --follow-tags
```

[1]: https://github.com/source-engine-discord/SourceEngine.Demo/releases/latest/download/IDemO_win-x64.zip
[2]: https://dotnet.microsoft.com/download/dotnet/5.0
[3]: https://visualstudio.microsoft.com/
[4]: https://www.jetbrains.com/rider/
[5]: https://code.visualstudio.com/
[6]: https://docs.microsoft.com/en-us/dotnet/core/deploying/index#self-contained-deployments-scd
[7]: https://docs.microsoft.com/en-us/dotnet/core/whats-new/dotnet-core-3-0#single-file-executables
[8]: https://docs.microsoft.com/en-us/dotnet/core/rid-catalog
[9]: https://github.com/StatsHelix/demoinfo/
[10]: Directory.Build.props
[11]: https://semver.org/spec/v2.0.0.html
[12]: https://github.com/Terri00/CSGODemoCSV
