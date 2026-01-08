# SVS-Fishbone — Copilot / AI Agent Instructions

This file captures the repository-specific details an AI coding agent needs to be productive quickly: architecture, workflows, conventions, and where to look for examples.

## Big picture
- Purpose: a tiny suite of BepInEx plugins + a CLI extractor that store plugin data inside PNGs using a private chunk named `fsBN`.
- Major components:
  - `Fishbone/` — core extension system (interfaces, serialization, Harmony hooks).
  - `CoastalSmell/` — runtime UI helpers used by plugins (not bundled into zips).
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
- Extension system
  - Use `SimpleExtension<T>` when char and coord schemas are identical; use `ComplexExtension<T,U>` when they differ.
  - Mark types with `[Extension(...)]`. The attribute accepts path components which are combined via `Path.Combine` to produce the Zip entry name (see `Fishbone/Internal.cs`).
  - Convenience helpers:
    - `Extension<T>.SerializeChara` / `DeserializeChara`
    - `Extension<T,U>.SerializeCoord` / `DeserializeCoord`
    - `Extension.OnPreprocessChara` and `OnPreprocessCoord` observables for translation hooks.
- PNG chunk format is single-source-of-truth in `Png.cs` — any encoder change must be validated against existing PNGs.
- Conditional compilation: projects set `<DefineConstants>` (game name) to switch behavior; code uses `#if` (eg. `Aicomi`, `DigitalCraft`) to handle platform differences.

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
  - Annotate with `[Extension("path","to","file.json")]` — the attribute value becomes the Zip entry path used by `Internal.cs`.
  - Use `Extension<MyExt>.SerializeChara` / `DeserializeChara` for IO and subscribe to `Extension.OnPreprocessChara` or `OnPreprocessCoord` when you need to translate or inject computed data.
- Add runtime assets: place files under `Resource/` and they will be copied by `Tasks.xml` on build.
- Changing packaging: update `Tasks.xml` `Release`/`Deploy` targets; verify resulting zip under `Release/`.

## Where to look (quick references) 📚
- `Png.cs` — encoding/decoding (`fsBN`) implementation.
- `Fishbone/Fishbone.cs` — interfaces, attributes, plugin bootstrap (and `Version` constant).
- `Fishbone/Internal.cs` — Zip entry naming, translation helpers, and Harmony patches.
- `Fishbone/*/` and `CoastalSmell/*/` — concrete extension examples and `Tasks.xml` deploy rules.
- `Extractor/Extractor.cs` — simple CLI: it reads file paths in `args` and writes the extracted zip next to the file.

---
If you want, I can: add a short example showing `SimpleExtension` vs `ComplexExtension`, or add a contributor checklist for releases and CI. Which section should I expand? ✅