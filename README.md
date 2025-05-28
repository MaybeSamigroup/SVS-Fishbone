# SVS-Fishbone

Plugin API to serialize and deserialize character or coordinate-bound extension data, for SamabakeScramble and DigitalCraft.

## Prerequisites

* [BepInEx](https://github.com/BepInEx/BepInEx)
  * v6.0.0 be 725 or later
* [ByteFiddler](https://github.com/BepInEx/BepInEx)
  * v1.0 or later and suitable configuration

Confirmed working under SamabakeScramble 1.1.6 and DigitalCraft 2.0.0.

## Installation

Extract the release to your game root directory.

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
- `OnPostCharacterDeserialize`
- `OnCharacterSerialize`
- `OnActorDeserialize`
- `OnActorSerialize`
- `OnPreActorHumanize`
- `OnPostActorHumanize`

### Coordinate Events

- `OnPreCoordinateDeserialize`
- `OnPostCoordinateDeserialize`
- `OnCoordinateSerialize`
- `OnPreCoordinateReload`
- `OnPostCoordinateReload`

Each event provides access to the relevant data and a `ZipArchive` representing the extension storage.

---

## Example: Subscribing to an Event

```csharp
Fishbone.Event.OnPostCharacterDeserialize += (human, limit, archive, storage) =>
{
    // Read or write your plugin's data in archive here
    // e.g., using archive.CreateEntry("YourPluginGuid/yourdata.bin")
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
