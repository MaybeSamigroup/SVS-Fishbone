# SVS-Fishbone — Copilot / AI Agent Instructions

This file captures the repository-specific details an AI coding agent needs to be productive quickly: architecture, workflows, conventions, and where to look for examples.

## Big picture
- Purpose: BepInEx plugins for Unity games (Aicomi, SamabakeScramble, DigitalCraft) that extend character and coordinate data by embedding serialized extensions in PNG files using a private chunk named `fsBN`.
- Major components:
  - `Fishbone/` — core extension system (interfaces, serialization, Harmony hooks).
  - `CoastalSmell/` — runtime UI helpers and reactive utilities used by plugins (not bundled into zips).
  - `Extractor/` — .NET 6 console app that extracts `fsBN` payloads and writes them as `<name>.zip` next to the source file.
  - `Png.cs` — canonical PNG chunk utilities. Use `Encode.Implant` to write and `Decode.Extract` to read.

## Build, deploy & packaging (practical workflows) ✅
- Projects target **.NET 6**.
- `Tasks.xml` (per project) provides convenience targets:
  - `Deploy` (runs for Debug builds): copies built DLL and `Resource/` assets into a local game install. Game path is read from registry key `HKEY_CURRENT_USER\Software\ILLGAMES\<GameName>`.
  - `Release` (runs for Release builds): stages `Release/<GameName>/...` and zips output into `./<AssemblyName>.zip`.
- Assembly naming: `$(GamePrefix)_$(PluginName)` (see `Tasks.xml` PropertyGroup).
- Override CI/local paths: pass MSBuild properties: `/p:GamePath="C:\Games\..." /p:GameName=SamabakeScramble`.
- Extractor usage: `dotnet run -c Release -- path\to\file.png [more-files]` — it reads each passed file and writes `<file>.zip` containing the `fsBN` payload (drag-and-drop passes files as args).

## Key patterns & idioms 🔧
- Extension system (Fishbone)
  - Extensions implement interfaces: `CharacterExtension<T>` for character data, `CoordinateExtension<U>` for coordinate data, or combine with `ComplexExtension<T,U>` (different schemas) or `SimpleExtension<T>` (identical schemas).
  - Mark types with `[ExtensionAttribute<T>(...)` or `[ExtensionAttribute<T,U>(...)]`. The attribute accepts path components combined via `Path.Combine` to produce the Zip entry name (see `Fishbone/Internal.cs`).
  - Convenience helpers:
    - `Extension<T>.SerializeChara` / `DeserializeChara` (Action<Stream, T> / Func<Stream, T>)
    - `Extension<T,U>.SerializeCoord` / `DeserializeCoord` for complex extensions
    - `Extension.OnPreprocessChara` and `OnPreprocessCoord` observables (IObservable) for translation hooks.
- PNG chunk format is single-source-of-truth in `Png.cs` — any encoder change must be validated against existing PNGs.
- Reactive Extensions (System.Reactive) used for observables and event handling.
- HarmonyLib for runtime patching of game code to intercept save/load operations.
- Conditional compilation: projects set `<DefineConstants>` (game name) to switch behavior; code uses `#if` (e.g., `Aicomi`, `DigitalCraft`) to handle platform differences.
- CoastalSmell utilities:
  - F class: functional helpers for currying (Apply), composition (With), error handling (Try), and collections (ForEach).
  - RxExtensions: wraps Il2Cpp observables (R3/UniRx) to System.Reactive for compatibility.
  - UGUI: declarative UI building with action combinators; sprites loaded from `Resource/`; windows with config-based toggles.
  - Json<T>: serialization with permissive options (trailing commas, comments, string numbers).
  - Float2/3/4: serializable structs for Unity Vector2/3/4 and Color.
  - Custom hooks: observables for human customization events (OnBodyChange, OnFaceChange, etc.) via Harmony patches.

## Examples of Extension Patterns
- **SimpleExtension**: Use when character and coordinate data share the same schema. Example:
  ```csharp
  [ExtensionAttribute<MyExt>("my", "data.json")]
  public class MyExt : SimpleExtension<MyExt>
  {
      public int Value { get; set; }
  }
  ```
  Access via `Extension<MyExt>.Humans[humanIndex]` or subscribe to `Extension.OnPreprocessChara` for loading hooks.

- **ComplexExtension**: Use when character and coordinate data have different schemas. Example:
  ```csharp
  [ExtensionAttribute<MyCharaExt, MyCoordExt>("my", "chara.json"), ("my", "coord.json")]
  public class MyCharaExt : ComplexExtension<MyCharaExt, MyCoordExt>, CharacterExtension<MyCharaExt>
  {
      public int CharaValue { get; set; }
      public MyCoordExt Get(int coordinateType) => /* logic */ ;
      public MyCharaExt Merge(int coordinateType, MyCoordExt mods) => /* merge logic */ ;
  }
  public class MyCoordExt : CoordinateExtension<MyCoordExt>
  {
      public int CoordValue { get; set; }
  }
  ```
  Access character data via `Extension<MyCharaExt, MyCoordExt>.Humans[humanIndex]`, coordinate via `.NowCoordinate[humanIndex]`.

## Testing & debugging (practical) ⚠️
- There are no unit tests; rely on:
  1) `Extractor` to validate that `fsBN` payloads are still extractable, and
  2) runtime testing by deploying to a local game install and checking `BepInEx\LogOutput.log`.
- When changing `Png.cs` or serialization formats:
  - Run `cd Extractor && dotnet run -c Release -- example.png` against representative PNGs.
  - Build Debug and confirm `Deploy` copied the plugin and resources to `$(GamePath)`; launch the game and inspect `LogOutput.log`.
- Bump plugin version: update `public const string Version` in `Fishbone/Fishbone.cs`.

## Common editing tasks (concise checklist) ✅
- Add an extension:
  - Implement `class MyExt : SimpleExtension<MyExt> { ... }` or `ComplexExtension<MyExt, MyCoordExt>`.
  - Annotate with `[ExtensionAttribute<T>("path","to","file.json")]` or `[ExtensionAttribute<T,U>(...)]` — the attribute value becomes the Zip entry path used by `Internal.cs`.
  - Use `Extension<MyExt>.SerializeChara` / `DeserializeChara` for IO and subscribe to `Extension.OnPreprocessChara` or `OnPreprocessCoord` when you need to translate or inject computed data.
- Add runtime assets: place files under `Resource/` and they will be copied by `Tasks.xml` on build.
- Changing packaging: update `Tasks.xml` `Release`/`Deploy` targets; verify resulting zip under `Release/`.

## Where to look (quick references) 📚
- `Png.cs` — encoding/decoding (`fsBN`) implementation.
- `Fishbone/Fishbone.cs` — interfaces, attributes, plugin bootstrap (and `Version` constant).
- `Fishbone/Internal.cs` — Zip entry naming, translation helpers, and Harmony patches.
- `Fishbone/*/` and `CoastalSmell/*/` — game-specific implementations and `Tasks.xml` deploy rules.
- `Extractor/Extractor.cs` — simple CLI: it reads file paths in `args` and writes the extracted zip next to the file.