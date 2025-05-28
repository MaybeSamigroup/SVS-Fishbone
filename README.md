# SVS-Fishbone

Plugin API to serialize and deserialize character or coordinate-bound extension data, for SamabakeScramble and DigitalCraft.

## Prerequisites

- [SVS-HF_Patch](https://github.com/BepInEx/BepInEx)

Confirmed working under SamabakeScramble 1.1.6 and DigitalCraft 2.1.0.

## Installation

Extract the release to your game install directory.

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
