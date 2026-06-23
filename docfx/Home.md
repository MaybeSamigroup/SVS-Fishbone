# Fishbone Extension Framework

Fishbone is a plugin framework for serializing (saving) and deserializing (loading) extended data in character and coordinate cards, currently for DigitalCraft, SamabakeScramble and Aicomi.

---

## Quick Start

- Register extension types in your plugin `Load`/`Unload`:

  ```csharp
  CompositeIDisposable Subscriptions;
  public override void Load() =>
      Subscription = [..Extension.Register<MyCharaExt, MyCoordExt>()];

  public override bool Unload() {
      Subscriptions.Dispose();
      return base.Unload();
  }
  ```

- Access stored data:

  ```csharp
  var ext = Extension<MyCharaExt, MyCoordExt>.Humans[human];
  var coordExt = Extension<MyCharaExt, MyCoordExt>.Humans[human, coordinateType];
  ```

## Conventions

- Use a plugin-specific prefix for attribute paths (e.g. `Plugin.Name`).
- Extended data is serialized to JSON by default using `System.Text.Json` (public properties only).
- Override serializers by assigning to `Extension<T, U>.SerializeChara`, `SerializeCoord`, or `Deserialize...` as needed.
- Convert older formats using `Extension<T, U>.Translate<OldType>(path, map)` before registering types.

---

## Extended Data Interfaces

A class or struct representing extended data must implement two or three of the following interfaces:

- The extension class `T` must have a parameterless constructor (`new()`).
  - For object extensions in DigitalCraft, there are no other restrictions.

- For character card extensions, `T` must implement `CharacterExtension<T>`, i.e., it must have the following method:

  ```csharp
  public T Merge(Character.HumanData.LoadLimited.Flags limit, T mods);
  ```

  This method is used by the framework when character data is loaded and merged in-game.  
  - The `limit` parameter corresponds to checkboxes in the character creation scene.
  - The `mods` parameter represents the loading data.
  - The implementation must merge the loading data (`mods`) with the current instance (`this`) and return the merged result.

  ![Character Merge UI](https://github.com/user-attachments/assets/b50038c4-2796-4d65-81fb-489f5c981f53)

  - If the extended data does not need to be saved in a coordinate card, there are no other restrictions.

- For coordinate card extensions, `T` must implement `CoordinateExtension<T>`, i.e., it must have the following method:

  ```csharp
  public T Merge(Character.HumanDataCoordinate.LoadLimited.Flags limit, T mods);
  ```

  This method is used by the framework when coordinate data is loaded and merged in-game.  
  - The `limit` parameter corresponds to checkboxes in the character creation scene.
  - The `mods` parameter represents the loading data.
  - The implementation must merge the loading data (`mods`) with the current instance (`this`) and return the merged result.

  ![Coordinate Merge UI](https://github.com/user-attachments/assets/4d5e2e22-a1aa-429d-bbbf-0632a1d3f439)

- Since character data contains multiple coordinate types and corresponding coordinate data, the extension class `T` for character cards and the corresponding extension class `U` for coordinate cards must implement `ComplexExtension<T, U>`, i.e., they must have the following methods:

  ```csharp
  public U Get(int coordinateType);
  ```

  Used by the framework to determine which data to save in the coordinate card or to provide the current coordinate data.  
  - The `coordinateType` parameter corresponds to the selection in the character creation scene.
  - The implementation must return the coordinate data extracted from the character data as result.

  ![Coordinate Type Selection](https://github.com/user-attachments/assets/80368ac4-9b71-4bec-87d3-ff96e59c8533)

  ```csharp
  public T Merge(int coordinateType, U mods);
  ```

  Used by the framework to apply loaded coordinate data to character data.  
  - The `coordinateType` parameter is the same as in `Get`.
  - The implementation must return the character data updated with the coordinate data.

  This interface is typically used for plugins with visual effects, such as SardineHead (MatricalEditor) or SardineTail (Softmod loader).

---

## Extended Data Location

The location of extended data in Fishbone's zip archive storage is declared by the following attributes:

- For `ComplexExtension<T, U>`:  
  - `ExtensionAttribute<T, U>`
- For `CharacterExtension<T>`:  
  - `ExtensionAttribute<T>`
- For object extension in DigitalCraft(`T where new()`):
  - `ObjectExtensionAttribute<T>`

The archive entry path will be the concatenation of the attribute's parameters (e.g., `foo/bar/modifications.json` from "foo", "bar", "modifications.json").
**To avoid conflicts with other plugins, a plugin name prefix is strongly recommended.**

### Typical Declaration of Extended Data For Character and Coordinate

```csharp
[Extension<YourDataForCharacter, YourDataForCoordinate>("foo", "bar", "modifications.json")]
public class YourDataForCharacter : CharacterExtension<YourDataForCharacter>, ComplexExtension<YourDataForCharacter, YourDataForCoordinate>, new()
{
    public YourDataForCharacter()
    {
        //...
    }
    public YourDataForCharacter Merge(Character.HumanData.LoadLimited.Flags limit, YourDataForCharacter mods)
    {
        //...
    }
    public YourDataForCoordinate Get(int coordinateType)
    {
        //...
    }
    public YourDataForCharacter Merge(int coordinateType, YourDataForCoordinate mods)
    {
        //...
    }
}
public class YourDataForCoordinate : CoordinateExtension<YourDataForCoordinate>, new()
{
     public YourDataForCoordinate()
    {
        //...
    }
    public YourDataForCoordinate Merge(Character.HumanDataCoordinate.LoadLimited.Flags limit, YourDataForCoordinate mods)
    {
        //...
    }
}
```

**Notes:**

- The extended data location of `ComplexExtension<T, U>` is common to both character cards and coordinate cards;
- Serialized data of type `T` is saved to the character card, and data of type `U` is saved to the coordinate card.

### Typical Declaration of Extended Data Only For Character

```csharp
[Extension<YourDataForCharacter>("foo", "bar", "modifications.json")]
public class YourDataForCharacter : CharacterExtension<YourDataForCharacter>
{
    public YourDataForCharacter()
    {
        //...
    }
    public YourDataForCharacter Merge(Character.HumanData.LoadLimited.Flags limit, YourDataForCharacter mods)
    {
        //...
    }
}
```

### Typical Declaration of Extended Data For Objects in DigitalCraft

```csharp
[ObjectExtension<YourDataForObjects>(TargetType.Item | TargetType.Folder, "foo", "bar", "modifications")]
public class YourDataForObjects : new()
{
    public YourDataForObjects()
    {
        //...
    }
}
```

**Notes:**

- In addition to its entry path in archive, object extension should declare its target type by enum composition of CoastallSmell.TargetType.
  - Currentlly available types for extensions are following:
    - TargetType.Item
    - TargetType.Light
    - TargetType.Folder
    - TargetType.Route
    - TargetType.Camera
- Object extensions are not stored directory in the declared path, but as a files under the path named according by its hierarchy in scene.
  - for first object in scene: foo/bar/modifications/1
    - for first child of above: foo/bar/modifications/1-1

---

## Extended Data Contents

By default, extended data is saved to and loaded from JSON format using [System.Text.Json.JsonSerializer](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/how-to).  

- Only public properties are saved/loaded by default.
- Plugins can override the default save/load operations by assigning alternatives to the following static fields

  - For `ComplexExtension<T, U>`:
    - `Extension<T, U>.SerializeChara`: `Action<Stream, T>`
    - `Extension<T, U>.SerializeCoord`: `Action<Stream, U>`
    - `Extension<T, U>.DeserializeChara`: `Func<Stream, T>`
    - `Extension<T, U>.DeserializeCoord`: `Func<Stream, U>`
  - For `CharacterExtension<T>`:
    - `Extension<T>.SerializeChara`: `Action<Stream, T>`
    - `Extension<T>.DeserializeChara`: `Func<Stream, T>`
  - For `ObjectExtension<T>`:
    - `ObjectExtension<T>.Serialize`: `Action<Stream, T>`
    - `ObjectExtension<T>.Deserialize`: `Func<Stream, T>`

---

## Registering Extended Data Types

Register extension classes using the following public static methods:

- For `ComplexExtension<T, U>`:
  - `Extension.Register<T, U>()`
- For `CharacterExtension<T>`:
  - `Extension.Register<T>()`
- For object extension in DigitalCraft(`T where new()`):
  - `Extension.RegisterObjecte<T>`

Use returned `IDisposable` to unregister extension.

Register and unregister in implementations of `BepInEx.Unity.IL2CPP.BasePlugin.Load` and `BepInEx.Unity.IL2CPP.BasePlugin.Unload`, for example:

```csharp
public class Plugin : BasePlugin
{
    CompositeDisposable Subscriptions;
    public override void Load() =>
        Subscriptions = [..Extension.Register<CharaMods, CoordMods>()];

    public override bool Unload() {
        Subscriptions.Dispose();
        return base.Unload();
    }
}
```

---

## Accessing Extended Data

After registering types, you can access extended data using the following public static indexers:

- For `ComplexExtension<T, U>`:
  - Indexers to set/get `T`
    - `Extension<T, U>.Humans[Character.Human]`
    - (SamabakeScramble only)
      - `Extension<T, U>.Actors[SaveData.Actor]`
      - `Extension<T, U>.Indices[int]`
    - (Aicomi only)
      - `Extension<T, U>.Actors[AC.User.ActorData]`
      - `Extension<T, U>.Indices[(int, int)]`
  - Indexers to set/get `U` of specific coordinate type
    - `Extension<T, U>.Humans[Character.Human, int]`
    - (SamabakeScramble only)
      - `Extension<T, U>.Actors[SaveData.Actor, int]`
      - `Extension<T, U>.Indices[int, int]`
    - (Aicomi only)
      - `Extension<T, U>.Actors[AC.User.ActorData, int]`
      - `Extension<T, U>.Indices[(int, int), int]`
  - Indexers to set/get `U` of current coordinate
    - `Extension<T, U>.Humans.NowCoordinate[Character.Human]`
    - (SamabakeScramble only)
      - `Extension<T, U>.Actors.NowCoordinate[SaveData.Actor]`
      - `Extension<T, U>.Indices.NowCoordinate[int]`
    - (Aicomi only)
      - `Extension<T, U>.Actors.NowCoordinate[AC.User.ActorData]`
      - `Extension<T, U>.Indices.NowCoordinate[(int, int)]`

- For `CharacterExtension<T>`:
  - Indexers to set/get `T`
    - `Extension<T>.Humans[Character.Human]`
    - (SamabakeScramble only)
      - `Extension<T>.Actors[SaveData.Actor]`
      - `Extension<T>.Indices[int]`
    - (Aicomi only)
      - `Extension<T>.Actors[AC.User.ActorData]`
      - `Extension<T>.Indices[(int, int)]`

- (DigitalCraft only)
  - For object extension in DigitalCraft(`T where new()`):
    - `ObjectExtension<T>.Values[DigitalCraft.ObjectInfo]`
    - `ObjectExtension<T>.Values[DigitalCraft.ObjectCtrlInfo]`

---

### Actor index (identifier) in SamabakeScramble

In game actors are identified by following property:

- SaveData.Actor.charasGameParam.Index
  - Type: `int`

### Actor index (identifier) in Aicomi

In game actors are identified by following property:

- (AC.User.ActorData.Guid.Group, AC.User.ActorData.Guid.Index)
  - Type: `(int, int)`

In game unique npcs are identified by following constants:

- (-1, 0): Teacher
- (-1, 1): Cafe Staff
- (-1, 2): Librarian

In free h actors are identified by following rules:

- (-2, n): nth male character
- (-3, n): nth female character

---

## Observable resources

A plugin can subscribe extended data related resources from the observables below.

### Observables at a glance

- `Extension.OnPreprocessChara` — `IObservable<(Character.HumanData, ZipArchive)>` — archive extracted from a character card or files
- `Extension.OnPreprocessCoord` — `IObservable<(Character.HumanDataCoordinate, ZipArchive)>` — archive extracted from a coordinate card or files
- `Extension<T,U>.OnPreprocessChara` / `Extension<T>.OnPreprocessChara` — deserialized extended data before binding
- `Extension.OnPrepareSaveChara` / `Extension.OnPrepareSaveCoord` — emitted when human is about to be saved (convert IDs, collect resources)
- `Extension.OnSaveChara` / `Extension.OnSaveCoord` — emitted when extension archive is about to be written to files
- `Extension.OnConvertChara` / `Extension.OnConvertCoord` — emitted when converting cards between games
- (DigitalCraft Only)
  - `Extension.OnInitScene` — `IObservable<Unit>` — scene initialized or just before scene load.
  - `Extension.OnLoadScene` — `IObservable<ZipArchive>` — scene loaded and archive extracted from scene file.
  - `Extension.OnImportScene` — `IObservable<ZipArchive>` — scene imported and archive extracted from scene file.
  - `Extension.OnSaveScene` — `IObservable<ZipArchive>` — scene extension is about to be be written to scene file.
  - `Extension.OnAddObject` — `IObservable<(TargetType, ObjectCtrlInfo)>` — object added in scene.
  - `Extension.OnDeleteObject` — `IObservable<(TargetType, ObjectCtrlInfo)>` — object deleted from scene.
  - `Extension.OnPrepareSaveObject` — `IObservable<(TargetType, ObjectCtrlInfo)>` — emitted when object is about to be saved (convert IDs, collect resources)
  - `Extension.OnPreprocessObject` — `IObservable<(TargetType, (ZipArchive, (int[], ObjectInfo)))>` — archive extracted from a scene file and bound to object info.
  - `ObjectExtension<T>.OnPreprocess` — `IObservable<(ObjectInfo, T)>` — deserialized extension bound to object info.
  - `ObjectExtension<T>.OnLoad` — `IObservable<(ObjectCtrlInfo, T)>` — deserialized extension bound to object ctrl info and be available in scene.

### Base data and extension archive loaded from files

- `Extension.OnPreprocessChara`
  - Type: `IObservable<(Character.HumanData Data, ZipArchive Archive)>`
  - Available in: DigitalCraft, SamabakeScramble, Aicomi

- `Extension.OnPreprocessCoord`
  - Type: `IObservable<(Character.HumanDataCoordinate Data, ZipArchive Archive)>`
  - Available in: DigitalCraft, SamabakeScramble, Aicomi

  *Description:*  
  Published when the Fishbone zip archive is extracted from a character/coordinate card or other external files.
  ZipArchive is provided in update mode and modifications made by subscribers reflected to later operations.
  
  *Use case:*  
  Plugins can convert older extended data to newer format by subscribing this resource.

  *Notes:*
  - Observable resources are published in **order of subscription**.
  - Subscription for older extended data conversion must done before extended data type registration; otherwise, conversion will run after when Fishbone already tried and failed to deserialize extended data.
  - There is a helper method to subscribe this resource for older extended data conversion:

    ```csharp
    namespace Fishbone {
        public static class Extension<T, U>
        {
            public static IDisposable Translate<V>(string path, Func<V, T> map) where V: new();
            public static IDisposable Translate<V>(string path, Func<V, U> map) where V: new();
        }
        public static class Extension<T>
        {
            public static IDisposable Translate<V>(string path, Func<V, T> map) where V: new();
        }
    }
    ```

    - Type parameter `V` represents the older extended data type.
    - Parameter `path` is the older extended data location.
    - Parameter `map` is the conversion function from older to newer data.
    - The returned `IDisposable` is used to unsubscribe from these resources.

---

### Base data and extended data deserialized from loaded archive

- `Extension<T, U>.OnPreprocessChara`  
  - Type: `IObservable<(Character.HumanData Data, T Value)>`
  - Available in: DigitalCraft, SamabakeScramble, Aicomi

- `Extension<T, U>.OnPreprocessCoord`  
  - Type: `IObservable<(Character.HumanDataCoordinate, U Value)>`
  - Available in: DigitalCraft, SamabakeScramble, Aicomi

- `Extension<T>.OnPreprocessChara`  
  - Type: `IObservable<(Character.HumanData, T)>`
  - Available in: DigitalCraft, SamabakeScramble, Aicomi

  *Description:*
  Published when extended data is deserialized from the Fishbone zip archive.

  *Use case:*
  SardineTail converts soft IDs in extended data to hard IDs by subscribing this resource.

  *Notes:*
  - Provided extended data is not yet bound to a human or actor.
  - Character and coordinate data may be sanitized before being bound to a human; use this to prevent that (e.g., all unavailable hard IDs are sanitized to 0).

---

### Actor bound to extended character data

- `Extension.OnLoadActorChara`  
  - Type: `IObservable<int>`
  - Available in: SamabakeScramble

- `Extension.OnLoadActorChara`  
  - Type: `IObservable<(int, int)>`
  - Available in: Aicomi

  *Description:*  
  Published when actor is bound to deserialized extended character data.

  *Notes:*  
  - Provided extended data is not yet bound to a human.

### Human bound to extended character data

- `Extension.OnLoadChara`
  - Type: `IObservable<Character.Human>`
  - Available in: DigitalCraft, SamabakeScramble, Aicomi

- `Extension.OnLoadCustoChara`  
  - Type: `IObservable<Character.Human>`
  - Available in: SamabakeScramble, Aicomi

- `Extension.OnActorHumanize`
  - Type: `IObservable<(Character.Human Human, int Index)>`
  - Available in: SamabakeScramble

- `Extension.OnActorHumanize`
  - Type: `IObservable<(Character.Human Human, (int, int) Index)>`
  - Available in: Aicomi

  *Description:*
  Published when human instance is bound to deserialized extended character data.

  *Use case:*  
  SardineHead starts applying modifications to the human by subscribing this resource.

  *Notes:*
  - In SamabakeScramble and Aicomi, `Extension.OnLoadChara` is merged resource of following
    - `Extension.OnLoadCustomChara`
    - `Extension.OnActorHumanize`

### Human bound to extended coordinate data

- `Extension.OnLoadCoord`
  - Type: `IObservable<Character.Human>`
  - Available in: DigitalCraft, SamabakeScramble, Aicomi

- `Extension.OnLoadCustomCoord`  
  - Type: `IObservable<Character.Human>`
  - Available in: SamabakeScramble, Aicomi

- `Extension.OnLoadActorCoord`  
  - Type: `IObservable<Character.Human>`
  - Available in: SamabakeScramble, Aicomi

  *Description:*
  Published when human instance is bound to deserialized extended coordinate data.

  *Use case:*  
  SardineHead starts applying modifications to the human by subscribing this resource.

  *Notes:*
  - This resource is published not only when loaded from files, but also when coordinate type has changed.
  - In SamabakeScramble and Aicomi, `Extension.OnLoadCoord` is merged resource of following
    - `Extension.OnLoadCustomCoord`
    - `Extension.OnLoadActorCoord`

### Human bound to extended character data which is about to serialized into archive

- `Extension.OnPrepareSaveChara`  
  - Type: `IObservable<Character.Human>`
  - Available in: DigitalCraft, SamabakeScramble, Aicomi

  *Description:*  
  In SamabakeScramble and Aicomi, published when a character card is about to be saved in Character Creation.
  In DigitalCraft, published when characters are about to be saved in scene data.

  *Use case:*  
  SardineTail converts hard IDs to soft IDs and stores them in extended data.

### Human bound to extended coordinate data which is about to serialized into archive

- `Extension.OnPrepareSaveCoord`  
  - Type: `IObservable<Character.Human>`
  - Available in: SamabakeScramble, Aicomi

  *Description:*  
  In SamabakeScramble and Aicomi, published when a coordinate card is about to be saved in Character Creation.

  *Use case:*  
  SardineTail converts hard IDs to soft IDs and stores them in extended data.

### Extension archive and human bound to extended character data which is about to be saved into files

- `Extension.OnSaveChara`
  - Type: `IObservable<(ZipArchive Archive, Character.Human Human)>`
  - Available in: DigitalCraft, SamabakeScramble, Aicomi

  *Description:*  
  In SamabakeScramble, published when extension archive is about to be saved into character card in custom scene.
  In DigitalCraft, published when extension archive is about to be saved into scene file.

  *Use case:*  
  SardineHead collects textures in use and stores them into archive separately from extended data by subscribing this resource.

### Extension archive and human bound to extended coordinate data which is about to be saved into files

- `Extension.OnSaveCoord`  
  - Type: `IObservable<(ZipArchive Archive, Character.Human Human)>`
  - Available in: SamabakeScramble, Aicomi

  *Description:*
  Published when extension archive is about to be saved into coordinate card in custom scene.
  
  *Use case:*  
  SardineHead collects textures in use and stores them into archive separately from extended data by subscribing this resource.

### Extension archive and actor bound to extended character data which is about to be saved into files

- `Extension.OnSaveActor`  
  - Type: `IObservable<(ZipArchive Archive, int Index)>`
  - Available in: SamabakeScramble

- `Extension.OnSaveActor`  
  - Type: `IObservable<(ZipArchive Archive, (int, int) Index)>`
  - Available in: Aicomi

  *Description:*  
  Published when extension archive is about to be saved into game's save file.

---

### Base data and extension archive which is about to serialized into another archive in conversion scene

- `Extension.OnConvertChara`  
  - Type: `IObservable<(ZipArchive Output, ZipArchive Input, Character.HumanData)>`
  - Available in: SamabakeScramble, Aicomi

- `Extension.OnConvertCoord`  
  - Type: `IObservable<(ZipArchive Output, ZipArchive Input, Character.HumanDataCoordinate)>`
  - Available in: SamabakeScramble, Aicomi

  *Description:*
  Published when character or coordinate card from another game has converted and saved into files.

  *Notes:*
  - Deserialized extended data is serialized into the file without being bound to human or actor.

## Partial Implementation Example (from SardineTail)

*Note: This example is illustrative and simplified; types and methods are from the SardineTail plugin.*

```csharp
using System.Reactive.Disposables;
using CharaLimit = Character.HumanData.LoadLimited.Flags;
using CoordLimit = Character.HumanDataCoordinate.LoadLimited.Flags;
using Fishbone;

namespace SardineTail
{
    public class CoordMods : CoordinateExtension<CoordMods>
    {
        public HairsMods Hairs { get; set; }
        public Dictionary<ChaFileDefine.ClothesKind, ClothMods> Clothes { get; set; }
        public Dictionary<int, AccessoryMods> Accessories { get; set; }
        public BodyMakeupMods BodyMakeup { get; set; }
        public FaceMakeupMods FaceMakeup { get; set; }

        public CoordMods Merge(CoordLimit limit, CoordMods mods) => new()
        {
            BodyMakeup = (limit & CoordLimit.BodyMakeup) == CoordLimit.None ? BodyMakeup : mods.BodyMakeup,
            FaceMakeup = (limit & CoordLimit.FaceMakeup) == CoordLimit.None ? FaceMakeup : mods.FaceMakeup,
            Accessories = (limit & CoordLimit.Accessory) == CoordLimit.None ? Accessories : mods.Accessories,
            Clothes = (limit & CoordLimit.Clothes) == CoordLimit.None ? Clothes : mods.Clothes,
            Hairs = (limit & CoordLimit.Hair) == CoordLimit.None ? Hairs : mods.Hairs,
        };
    }

    [Extension<CharaMods, CoordMods>(Plugin.Name, "modifications.json")]
    public class CharaMods : CharacterExtension<CharaMods>, ComplexExtension<CharaMods, CoordMods>
    {
        public ModInfo Figure { get; set; }
        public ModInfo Graphic { get; set; }
        public FaceMods Face { get; set; }
        public BodyMods Body { get; set; }
        public Dictionary<ChaFileDefine.CoordinateType, CoordMods> Coordinates { get; set; }

        public CharaMods Merge(CharaLimit limit, CharaMods mods) => new()
        {
            Figure = (limit & CharaLimit.Body) == CharaLimit.None ? Figure : mods.Figure,
            Body = (limit & CharaLimit.Body) == CharaLimit.None ? Body : mods.Body,
            Face = (limit & CharaLimit.Face) == CharaLimit.None ? Face : mods.Face,
            Graphic = (limit & CharaLimit.Graphic) == CharaLimit.None ? Graphic : mods.Graphic,
            Coordinates = (limit & CharaLimit.Coorde) == CharaLimit.None ? Coordinates : mods.Coordinates,
        };

        public CoordMods Get(int coordinateType) =>
            Coordinates?.GetValueOrDefault((ChaFileDefine.CoordinateType)coordinateType) ?? new();

        public CharaMods Merge(int coordinateType, CoordMods mods) => new()
        {
            Figure = Figure,
            Body = Body,
            Face = Face,
            Graphic = Graphic,
            Coordinates = (Coordinates ?? new()).Merge((ChaFileDefine.CoordinateType)coordinateType, mods)
        };
    }

    public class Plugin : BasePlugin
    {
        CompositeDisposable Subscriptions;
        public override void Load() => Subscriptions = [
          Extension<CharaMods, CoordMods>.Translate<CharaMods>(Path.Combine(Guid, "modifications.json"), mods => mods),
          Extension<CharaMods, CoordMods>.Translate<CoordMods>(Path.Combine(Guid, "modifications.json"), mods => mods),
          ..Extension.Register<CharaMods, CoordMods>(),
          Extension<CharaMods, CoordMods>.OnPreprocessChara.Subscribe(tuple => tuple.Value.Apply(tuple.Data)),
          Extension<CharaMods, CoordMods>.OnPreprocessCoord.Subscribe(tuple => tuple.Value.Apply(tuple.Data)),
          Extension.OnPrepareSaveChara.Subscribe(_ => IOExtension.SaveCustomChara()),
          Extension.OnPrepareSaveCoord.Subscribe(_ => IOExtension.SaveCustomCoord()),
          ..Extension.RegisterConversion<CharaMods, CoordMods>()
        ]
        public override bool Unload() {
            Subscriptions.Dispose();
            return base.Unload();
        }
    }
}
```

**Notes:**

- There is a helper method for dictionary-based extended data to implement `ComplexExtension<T, U>.Merge(int, U)`:

  ```csharp
  namespace Fishbone {
      public static class Extension {
          // Coordinates = (Coordinates ?? new()).Merge((ChaFileDefine.CoordinateType)coordinateType, mods)
          public Dictionary<K, V> Merge<K, V>(this Dictionary<K, V> mods, K index, V mod);
      }
  }
  ```
