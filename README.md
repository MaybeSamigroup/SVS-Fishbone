# SVS-Fishbone

Plugin API to serialize and deserialize character or coordinate-bound extension data, for SamabakeScramble and DigitalCraft.

## Prerequisites (SVS)

- [HF Patch for Summer Vacation Scramble](https://github.com/ManlyMarco/SVS-HF_Patch)

Confirmed working under SamabakeScramble 1.1.6 and DigitalCraft 2.1.0.

## Installation (SVS)

Extract the [latest release](https://github.com/MaybeSamigroup/SVS-Fishbone/releases/latest) SamabakeScramble.zip to your SamabakeScramble install directory.


## Installation (DC with HC)

Apply latest [HF patch for HoneyCome and DigitalCraft](https://github.com/ManlyMarco/HC-HF_Patch)

Extract the [latest release](https://github.com/MaybeSamigroup/SVS-Fishbone/releases/latest) DigitalCraft.zip to your HoneyCome install directory.

## Installation (DC Standalone)

For the first words, I can't figure out valid version of BepinEx bleeding edge release for Digital Craft 2.1.0.

Then this instruction uses  SVS-HF_Patch 1.7 content for alternative.

Copy these files and directories from SVS install directory to DC install directory:

1. Entire direcory: dotnet
1. Entire direcory: BepInEx\core
1. Single file: BepInEx\config\BepInEx.cfg

Modify BepInEx.cfg;  rewrite GolobalMetadataPath of to default, because Digital Craft's metadata is not encrypted.

```diff
# Default value: {GameDataPath}/il2cpp_data/Metadata/global-metadata.dat
-GlobalMetadataPath = {BepInEx}/{ProcessName}/decrypted_global-metadata.dat
+GlobalMetadataPath = {GameDataPath}/il2cpp_data/Metadata/global-metadata.dat
```

Copy these files from SVS install directory to where DC executable placed directory. (directory named DigitalCraft under install directory; where you found DigitalCraft.exe)

1. .doorstop_version
1. hid.dll
1. doorstop_config.ini

Modify doorstop_config.ini to fit directory structure of  Digital Craft.

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

Make these directories under Digital Craft install directory and place patchers and plugins you want to apply.

1. BepInEx\patchers
1. BepInEx\plugins

Extract the [latest release](https://github.com/MaybeSamigroup/SVS-Fishbone/releases/latest) DigitalCraft.zip to your DigitalCraft install directory.

As final state, you should have following directories and files in Digital Craft install directory.

1. dotnet
1. BepInEx\core
1. BepInEx\config\BepInEx.cfg
1. DigitalCraft\.doorstop_version
1. DigitalCraft\doorstop_config.ini
1. DigitalCraft\hid.dll
1. BepInEx\patchers
1. BepInEx\plugins\DC_SardineTail.dll

## Migration from 1.x.x release

Remove Fishbone.dll from BepinEx/plugins.

Plugin assembly names are now SVS_Fishbone.dll and DC_Fishbone.dll.

Cards made with 1.x.x is still supported to load in SVS, but not in DC.

To use them in DC, should saved agein with 2.x.x.

## How it works

Fishbone extensions are embedded into game character or coordinate portraits as a PNG private extension chunk named `"fsBN"`.  
This allows arbitrary plugin data to be stored and retrieved alongside character or coordinate files, visible in the file explorer.

## How to use

Plugin authors who want to use Fishbone should subscribe to any of the provided events.

Extension storages are provided as `ZipArchive`, shared by all plugins.  
**It is strongly advised to use each plugin's GUID as a directory name for your data, and to organize sub-entries accordingly.**

---

## Provided Events

Fishbone exposes a set of events for both character and coordinate serialization/deserialization.  
These events allow plugins to read or write extension data at various points in the character/coordinate lifecycle.

### Character Events

- `OnPreCharacterDeserialize`
  - SVS and DC
- `OnPostCharacterDeserialize`
  - SVS and DC
- `OnCharacterSerialize`
  - SVS Only
- `OnActorDeserialize`
  - SVS Only
- `OnActorSerialize`
  - SVS Only
- `OnPreActorHumanize`
  - SVS Only
- `OnPostActorHumanize`
  - SVS Only

### Coordinate Events

- `OnPreCoordinateDeserialize`
  - SVS and DC
- `OnPostCoordinateDeserialize`
  - SVS and DC
- `OnCoordinateSerialize`
  - SVS Only
- `OnPreCoordinateReload`
  - SVS and DC
- `OnPostCoordinateReload`
  - SVS and DC

Each event provides access to the relevant data and a `ZipArchive` representing the extension storage.

---

## Example: Subscribing to an Event

```csharp
Fishbone.Event.OnPostCharacterDeserialize += (human, limit, archive, storage) =>
{
    // In this case, archive contains extension from deserialized card.
    // Acoording to limit, update your plugin's data in storage here.
    // e.g., using storage.CreateEntry("YourPluginGuid/yourdata.bin")
};
```

---

## Notes

- Fishbone is compatible with both SamabakeScramble and DigitalCraft, with process-specific logic for each.
- Extension data is preserved across character and coordinate saves/loads.
- Use the provided events to ensure your plugin data is always synchronized with the game state.

---

## License

See [LICENSE](LICENSE) for details.

---
