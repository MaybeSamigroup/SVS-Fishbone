# SVS-Fishbone

## Table of Contents

- [Fishbone](#fishbone)
- [CoastalSmell](#coastalsmell)
- [Extractor](#extractor)
- [Prerequisites and Installation](#prerequisites-and-installation)
  - [Aicomi](#aicomi)
  - [SamabakeScramble](#samabakescramble)
  - [DigitalCraft](#digitalcraft-standalonedigitalcraft-with-honeycome)
- [Migration between versions](#migration-between-versions)
- [How It Works](#how-it-works)
- [How to Use](#how-to-use)

## Fishbone

Plugin API to serialize and deserialize character or coordinate-bound extension data, for Aicomi, SamabakeScramble and DigitalCraft.

## CoastalSmell

Plugin API to provide syntax sugars, common observable resources, and runtime UnityUI component generators, for Aicomi, SamabakeScramble and DigitalCraft.

Binary releases contain required [Reactive Extensions for .NET](https://github.com/dotnet/reactive) distributed under MIT license.

## Extractor

Extension data extractor from Drag & Dropped Characacter and Coordinate cards.

Extension data will be extracted as zip file with the same name in the same folder as source file.

## Prerequisites and Installation

### Aicomi

Confirmed working under Aicomi 1.0.7.

#### Prerequisites (Aicomi)

- [HF Patch for Aicomi](https://github.com/ManlyMarco/AC-HF_Patch)

#### Installation (Aicomi)

Extract the [latest release](https://github.com/MaybeSamigroup/SVS-Fishbone/releases/latest) AC_Fishbone.zip to your Aicomi install directory.

### SamabakeScramble

Confirmed working under SamabakeScramble 1.1.6.

#### Prerequisites (SamabakeScramble)

- [HF Patch for Summer Vacation Scramble](https://github.com/ManlyMarco/SVS-HF_Patch)

#### Installation (SamabakeScramble)

Extract the [latest release](https://github.com/MaybeSamigroup/SVS-Fishbone/releases/latest) SVS_Fishbone.zip to your SamabakeScramble install directory.

### DigitalCraft Standalone/DigitalCraft with HoneyCome

Confirmed working under DigitalCraft Standalone 3.0.0.

#### Prerequisites (DigitalCraft)

- [BepInEx](https://github.com/BepInEx/BepInEx)
  - [Bleeding Edge (BE) build](https://builds.bepinex.dev/projects/bepinex_be) #752 or later

#### Installation (DigitalCraft)

Extract BepInEx bleeding edge build into DigitalCraft install directory.

Move these files from install directory to directory where the executable is placed. (directory named DigitalCraft under install directory; where you found DigitalCraft.exe)

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

Extract the [latest release](https://github.com/MaybeSamigroup/SVS-Fishbone/releases/latest) DC_Fishbone.zip to your DigitalCraft install directory.

## Migration between versions

### Migration from 1.x.x to 2.x.x

Remove Fishbone.dll from BepinEx/plugins.

Plugin assembly names are now SVS_Fishbone.dll and DC_Fishbone.dll.

Cards made with 1.x.x are still supported to load in SVS, but not in DC.

To use them in DC, should be saved agein with 2.x.x.

### Migration from 2.x.x to 3.x.x

These directories contained in previous releases are no longer used.
Please move it contents to new one and delete it.

- (GameRoot)/UserData/plugins/SamabakeScramble.CoastalSmell

## How it works

Fishbone extensions are embedded into game character or coordinate portraits as a PNG private extension chunk named `"fsBN"`.  
This allows arbitrary plugin data to be stored and retrieved alongside character or coordinate files, visible in the file explorer.

## How to use

Refer to the [Framework introduction](https://github.com/MaybeSamigroup/SVS-Fishbone/wiki)
