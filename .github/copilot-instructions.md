# SVS-Fishbone — Copilot / AI Agent Instructions

This file captures repository-specific context and conventions to help AI coding agents be immediately productive.

## Big picture
- Purpose: a small suite of BepInEx plugins and a CLI extractor to store and extract extension data embedded into PNGs for multiple games (Aicomi / SamabakeScramble / DigitalCraft).
- Major components:
  - Fishbone (core): serialization APIs and extension system (see `Fishbone/Fishbone.cs`, `Png.cs`).
  - CoastalSmell: runtime UI helpers and resources (`CoastalSmell/*`, `CoastalSmell/Resource/*`).
  - Game-specific assemblies: AC_Fishbone, SVS_Fishbone, DC_Fishbone — each defines the `Process` and `GameName` for the target game (`Fishbone/*/{AC,SVS,DC}` folders).
  - Extractor: console tool that reads PNGs and extracts the embedded extension ZIPs (`Extractor/`).
- Why: Plugins embed arbitrary extension data as a PNG private chunk named `"fsBN"` (see `Png.cs`) so mod data travels with character/coordinate files.

## How to build & package (developer workflow)
- Normal (Debug deploy to local install via registry lookup):
  - dotnet build -c Debug for the target csproj (e.g. `cd Fishbone/SVS && dotnet build -c Debug`) — the `Tasks.xml` file will copy the plugin DLL to `$(GamePath)\BepInEx\plugins\` and copy runtime helpers to `$(GamePath)\dotnet\` using `$(GamePath)` resolved from the registry (HKEY_CURRENT_USER\Software\ILLGAMES\<GameName>). See `Fishbone/Tasks.xml` and `CoastalSmell/Tasks.xml`.
- Release (packaged zip):
  - dotnet build -c Release will create `Release/<GameName>/...` and then zip to `./<AssemblyName>.zip` as defined in `Tasks.xml` (e.g. `SVS_Fishbone.zip`).
- Overrides and troubleshooting:
  - If registry entries aren't present (or you want to target a specific game install), pass MSBuild properties: e.g.
    - dotnet build path/to/SVS_Fishbone.csproj -c Debug /p:GamePath="C:\Games\SamabakeScramble" /p:GameName=SamabakeScramble
  - Compiling may require the game's BepInEx interop DLLs to satisfy HintPath references. Ensure the game's `BepInEx/<GameName>/interop` exists (used by `DC`/`SVS` projects).
- Extractor: build and run the console app in `Extractor/`; release packaging is handled by the project `Release` target (zips to `Extractor.zip`).

## Important repo-specific conventions & patterns
- **PNG chunk format:** `Png.cs` implements the fsBN private chunk handling.
  - Use `Encode.Implant(...)` to embed, `Decode.Extract(...)` to read — changing this affects runtime and Extractor behavior.
- **Extension system:** Implement `SimpleExtension<T>` or `ComplexExtension<T,U>`, annotate with `[Extension("path","to","entry")]`. Use `Extension<T>.SerializeChara` / `DeserializeChara` and `Extension<T>.Translate(...)` to hook into load-time translation.
- **Project/reference conventions:**
  - Each game target defines `GameName` and `Process` constants (see `Fishbone/SVS/*`, `Fishbone/AC/*`, `Fishbone/DC/*`); they are referenced by `[BepInProcess(Process)]` attributes.
  - `ProjectReference` to `CoastalSmell` is intentionally non-embedded (`<Private>False</Private>`, `<ExcludeAssets>all</ExcludeAssets>`). Do not assume CoastalSmell will be bundled into the plugin zip.
  - Shared files (e.g., `Png.cs`, `Fishbone.cs`) are compiled into multiple projects via `<Compile Include="..\Png.cs"/>` — edit with care.
- **Packaging:** `Tasks.xml` controls Debug `Deploy` (copies to `$(GamePath)` using registry) and Release `Zip` behavior (produces `Release/<GameName>/...` and `<AssemblyName>.zip`).

## Testing & debugging hints
- Check runtime logs at `\<GamePath>\BepInEx\LogOutput.log` for stack traces and plugin load errors.
- UI or hook changes usually require launching the target game with BepInEx to validate behavior.
- There are no automated unit tests — prefer small, manual integration tests (Debug build → Deploy → run game) or exercising `Extractor` against representative PNGs.

## Common editing tasks and pointers for patching
- Add serialized extension: create a class implementing the right extension interface, annotate with `[Extension(...)]`, and wire up translation with `Extension<T>.Translate` if you need to map legacy formats.
- Modify PNG chunk format: update `Png.cs` **and** `Extractor` simultaneously. Extractor looks for the fsBN chunk and writes `<file>.zip` next to the PNG.
- Add UI resource: place textures in `CoastalSmell/Resource/`; `CoastalSmell/Tasks.xml` copies them to `UserData/plugins/CoastalSmell` on Deploy.
- Bump version: update `Plugin.Version` constants (e.g., in `Fishbone/Fishbone.cs`, `CoastalSmell/CoastalSmell.cs`) and confirm zip naming in the Release target.

## CI / cross-machine notes
- Development builds depend on registry entries `HKEY_CURRENT_USER\Software\ILLGAMES\<GameName>` for `GamePath`. For CI provide `/p:GamePath` and `/p:GameName` to avoid workstation registry dependence.
- Projects target **.NET 6** — ensure .NET 6 SDK is present on CI agents.

## Quick commands (cheat sheet)
- Build & deploy debug to a game (uses registry):
  - cd Fishbone/SVS && dotnet build -c Debug
- Build release and create zip:
  - cd Fishbone/SVS && dotnet build -c Release
- Override for CI/local test:
  - dotnet build path/to/SVS_Fishbone.csproj -c Debug /p:GamePath="C:\Games\SamabakeScramble" /p:GameName=SamabakeScramble
- Extractor examples:
  - Build: `cd Extractor && dotnet build -c Release`
  - Run: `dotnet run --project Extractor -- path\to\chara.png` → creates `chara.zip` next to `chara.png`.

## Where to look (short map)
- Serialization & extension API: `Fishbone/Fishbone.cs`
- PNG encoder/decoder: `Png.cs`
- Extractor CLI: `Extractor/Extractor.cs` and `Extractor.csproj`
- UI helpers & resources: `CoastalSmell/*` and `CoastalSmell/Resource/*`
- Game-specific patches: `Fishbone/*/{AC,SVS,DC}/*_Internal.cs`

---
If anything above is unclear or you'd like short, copy-pasteable code examples (e.g., `SimpleExtension<T>` template, `dotnet` commands for CI or Release steps, or a safe `Png.cs` change checklist), tell me which area to expand and I will iterate. ✅