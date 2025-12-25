# SVS-Fishbone — Copilot / AI Agent Instructions

This file captures repository-specific context and conventions to help AI coding agents be immediately productive. It focuses on the concrete patterns and build workflows you will encounter working in this repository.

## Big picture
- Purpose: a small suite of BepInEx plugins and a CLI extractor that embed and extract extension data inside PNGs for multiple games (Aicomi / SamabakeScramble / DigitalCraft).
- Major components:
  - `Fishbone/` — core serialization and extension system (see `Fishbone/Fishbone.cs`) and per-game projects (`Fishbone/AC`, `Fishbone/DC`, `Fishbone/SVS`).
  - `CoastalSmell/` — runtime UI helpers and per-game helper projects referenced by plugins (not bundled into zips; see `ProjectReference` flags).
  - `Extractor/` — net6 console app that validates and extracts `fsBN` chunks from PNGs (links `../Png.cs`).
  - `Png.cs` (repo root) — canonical PNG chunk utilities. The private chunk name is `fsBN`; use `Encode.Implant` to write and `Decode.Extract` to read.

## Build, deploy & packaging (practical workflows)
- Projects target .NET 6 (see `TargetFramework` in csproj files).
- Debug local deploy (convenience deploys into game install):
  - `cd Fishbone/SVS && dotnet build -c Debug` — `Tasks.xml` has a `Deploy` target that copies the plugin DLL to `$(GamePath)\BepInEx\plugins` and any `Resource/` artifacts to `$(GamePath)\dotnet` or `UserData/plugins`.
  - `$(GamePath)` is resolved from registry: `HKEY_CURRENT_USER\Software\ILLGAMES\<GameName>` by default.
- Release packaging:
  - `dotnet build -c Release` will stage files under `Release/<GameName>/...` and each project zips its release folder into `./<AssemblyName>.zip` (see `Release` target in `Tasks.xml` and `Extractor.csproj`).
- Overrides / CI usage:
  - Provide `GamePath` or `GameName` via MSBuild properties: `dotnet build path/to/SVS_Fishbone.csproj -c Debug /p:GamePath="C:\Games\SamabakeScramble" /p:GameName=SamabakeScramble`.
- Run the Extractor locally:
  - `cd Extractor && dotnet run -c Release` — useful to validate `fsBN` payloads and ensure compatibility after changes to `Png.cs`.

## Important repo-specific conventions & patterns
- PNG encoding: the canonical methods are `Encode.Implant` and `Decode.Extract` in `Png.cs`. Many tests are manual: if you change the encoder you must run `Extractor` against representative PNGs.
- Extension system:
  - Use `SimpleExtension<T>` for extensions where character & coordinate data share schema, or `ComplexExtension<T,U>` when they differ.
  - Annotate with `[Extension("path/to/entry")]` (see existing examples in `Fishbone/*`).
  - Serialization helpers: `Extension<T>.SerializeChara`, `DeserializeChara`, `SerializeCoord`, `DeserializeCoord` (backed by `Json<T>.Save/Load`).
  - Translation helpers subscribe to `Extension.OnPreprocessChara` / `OnPreprocessCoord` and use `ZipArchive` entries for extension payloads.
- Packaging nuance: `CoastalSmell` projects set `<Private>False</Private>` and `<ExcludeAssets>all</ExcludeAssets>` on the `ProjectReference`, so runtime helpers are expected to be present in the target game install and are not bundled into plugin zips.
- Game-specific configuration: each game project sets `<GameName>` (e.g., `SamabakeScramble`) and `GamePath` is typically derived from the registry. `DefineConstants` is set to the game name for conditional compilation where used.

## Testing & debugging hints
- No unit tests are included; rely on: 1) `Extractor` to validate PNG payload formats, and 2) runtime testing by launching the target game with BepInEx.
- Runtime logs: check `<GamePath>\BepInEx\LogOutput.log` for plugin load/runtime errors and `Plugin.Version` (e.g., in `Fishbone/Fishbone.cs`) for versioning.
- When modifying `Png.cs` or extension serialization:
  - Run `cd Extractor && dotnet run -c Release` against representative PNGs.
  - Build and `dotnet build -c Debug` plugin and verify the `Deploy` target copied artifacts into the local game install; launch the game to reproduce any integration issues.

## Common editing tasks (explicit examples)
- Add a new serialized extension:
  - Implement `class MyExt : SimpleExtension<MyExt> { ... }` or `ComplexExtension<MyExt, MyCoordExt>` as appropriate.
  - Add `[Extension("char/myext")]` above the class; implement `Merge(...)` and `Get(...)` per interface contract.
  - Use `Extension<MyExt>.SerializeChara(stream, value)` to write payloads and `Extension<MyExt>.DeserializeChara(stream)` to read.
- Change PNG encoding: update `Png.cs`, then use `Extractor` to verify that previously produced `fsBN` chunks are readable.
- Add runtime assets: place them under the project's `Resource/` folder; `Tasks.xml` will copy them during `Deploy`/`Release` targets.
- Bump plugin version: update `public const string Version` in the plugin `Plugin` class (e.g. `Fishbone/Fishbone.cs`), rebuild and ensure zip names in `Release/` reflect changes.

## Where to look (quick references)
- `Png.cs` — canonical chunk encoding/decoding (fsBN).
- `Fishbone/Fishbone.cs` — extension interfaces, helpers, and plugin entry point.
- `Fishbone/*/` and `CoastalSmell/*/` — per-game implementations and `Tasks.xml` deploy/release rules.
- `Extractor/Extractor.cs` & `Extractor.csproj` — CLI validator and the release zip rule.
- `Release/` — example artifacts created by the `Release` targets.

## Quick commands (cheat sheet)
- Build & deploy debug (uses registry):
  - `cd Fishbone/SVS && dotnet build -c Debug`
- Build release and create zip:
  - `cd Fishbone/SVS && dotnet build -c Release`
- Override install location for local testing:
  - `dotnet build path/to/SVS_Fishbone.csproj -c Debug /p:GamePath="C:\Path\To\Game" /p:GameName=SamabakeScramble`
- Run/validate extractor:
  - `cd Extractor && dotnet run -c Release`

---
If you'd like, I can tighten any section (add concrete code examples for `SimpleExtension` vs `ComplexExtension`, add a short contributor checklist for releases, or dock CI examples); tell me which areas to expand and I will iterate. ✅