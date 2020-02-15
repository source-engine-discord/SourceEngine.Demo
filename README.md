# IDemO
Extended from CSGODemoCSV, utilises DemoInfo.

Latest release **v1.1.5**: https://github.com/JamesT-W/IDemO/releases/tag/v1.1.5

![Program output](https://i.imgur.com/RALmTAR.png)

## Supported Gamemodes
- Defuse
- Hostage
- Wingman

## Example Usage

Example: `IDemO -folders "demos" -output "parsed" -recursive -nochickens -samefilename -samefolderstructure`
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
