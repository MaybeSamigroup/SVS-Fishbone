# SVS-Fishbone

## Fishbone

Plugin API to serialize and deserialize character or coordinate-bound extension data, for Aicomi, SamabakeScramble and DigitalCraft.

## CoastalSmell

Plugin API to generate UnityUI component in runtime and syntax suggers, for Aicomi, SamabakeScramble and DigitalCraft.

## Extractor

Extension data extractor from Drag & Dropped Chracacter and Coordinate cards.

Extension data will extracted as same name zip file in same folder of source file.

## Prerequisites (Aicomi)

- [BepInEx](https://github.com/BepInEx/BepInEx)
  - [Bleeding Edge (BE) build](https://builds.bepinex.dev/projects/bepinex_be) #738 or later

Confirmed working under Aicomi 1.0.1.

## Prerequisites (SamabakeScramble)

- [HF Patch for Summer Vacation Scramble](https://github.com/ManlyMarco/SVS-HF_Patch)

Confirmed working under SamabakeScramble 1.1.6.

## Installation (SamabakeScramble)

Extract the [latest release](https://github.com/MaybeSamigroup/SVS-Fishbone/releases/latest) SamabakeScramble.zip to your SamabakeScramble install directory.

## Prerequisites (DigitalCraft with HoneyCome)

- [HF patch for HoneyCome and DigitalCraft](https://github.com/ManlyMarco/HC-HF_Patch)

## Installation (DigitalCraft with HoneyCome)

Extract the [latest release](https://github.com/MaybeSamigroup/SVS-Fishbone/releases/latest) DigitalCraft.zip to your HoneyCome install directory.

## Prerequisites (DigitalCraft Standalone)

Confirmed working under DigitalCraft 2.0.0.

- [BepInEx](https://github.com/BepInEx/BepInEx)
  - [Bleeding Edge (BE) build](https://builds.bepinex.dev/projects/bepinex_be) #697 or later

## Installation (DigitalCraft Standalone)

Extract BepinEx bleeding edge build into Digital Craft install directory.

Move these files from install directory to where executable placed directory. (directory named DigitalCraft under install directory; where you found DigitalCraft.exe)

1. .doorstop_version
1. winhttp.dll
1. doorstop_config.ini

Modify doorstop_config.ini to fit directory structure of Digital Craft.

```diff
[General]

# Enable Doorstop?
enabled = true

# Path to the assembly to load and execute
# NOTE: The entrypoint must be of format `static void Doorstop.Entrypoint.Start()`
-target_assembly = BepInEx\core\BepInEx.Unity.IL2CPP.dll
+target_assembly = ..\BepInEx\core\BepInEx.Unity.IL2CPP.dll
```

```diff
[Il2Cpp]

# Path to coreclr.dll that contains the CoreCLR runtime
-coreclr_path = dotnet\coreclr.dll
+coreclr_path = ..\dotnet\coreclr.dll

# Path to the directory containing the managed core libraries for CoreCLR (mscorlib, System, etc.)
-corlib_dir = dotnet
+corlib_dir = ..\dotnet
```

Extract the [latest release](https://github.com/MaybeSamigroup/SVS-Fishbone/releases/latest) DigitalCraft.zip to your DigitalCraft install directory.

## Migration from 1.x.x to 2.x.x

Remove Fishbone.dll from BepinEx/plugins.

Plugin assembly names are now SVS_Fishbone.dll and DC_Fishbone.dll.

Cards made with 1.x.x is still supported to load in SVS, but not in DC.

To use them in DC, should saved agein with 2.x.x.

## Migration from 2.x.x to 3.x.x

These directories contained in previous releases are no longer used.
Please move it contents to new one and delete it.

- (GameRoot)/UserData/plugins/SamabakeScramble.CoastallSmell

## How it works

Fishbone extensions are embedded into game character or coordinate portraits as a PNG private extension chunk named `"fsBN"`.  
This allows arbitrary plugin data to be stored and retrieved alongside character or coordinate files, visible in the file explorer.

## How to use

refer the [Framework introduction](https://github.com/MaybeSamigroup/SVS-Fishbone/wiki)
