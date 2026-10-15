# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A BepInEx 6 plugin (HarmonyX-based mod) for the Unity game **活俠傳 / Legend of Mortal** (Unity 2020.3.49, Mono). It adds an in-game IMGUI cheat/debug menu (toggled with `F3`, configurable via BepInEx config key `LegendOfMortalMod.MenuToggleKey`). UI strings and many game identifiers (e.g. `GameStatType.體力`) are Traditional Chinese — keep them as-is.

## Build

- SDK-style `LegendOfMortalMod.csproj`, `net472`, `LangVersion latest`: `dotnet build -c Release`
- After building, the DLL is copied to `$(GamePath)\BepInEx\plugins\LegendOfMortalMod\` if that folder exists. `GamePath` defaults to the Steam install (`C:\Program Files (x86)\Steam\steamapps\common\LegendOfMortal`); pass `-p:GamePath=NUL` to build without deploying.
- Formatting: CSharpier — `dotnet tool restore` then `dotnet csharpier .`
- No tests. Verify by launching with `explorer.exe "steam://rungameid/1859910"` and reading `<game>\BepInEx\LogOutput.log`. It should list `Loading [活俠傳修改選單 1.0.0]` and no Harmony errors. Check the log's timestamp first: when BepInEx doesn't start, the file is not rewritten and you read the previous run's log.

## Game/loader gotchas

- Never start `Mortal.exe` directly. The game restarts itself through Steam, and if Steam wasn't running it inherits the injected game's `DOORSTOP_INITIALIZED` environment variable. Every later Steam launch then skips Doorstop and BepInEx never starts (log not rewritten, `winhttp.dll` still loaded). Fix: fully exit Steam and reopen it normally.
- `BepInEx.cfg` `[Preloader.Entrypoint] Type` must be `Camera`, not the default `Application`. With `Application` the chainloader runs before the engine is ready (`Screen.width` reports ~298) and the plugin is destroyed when the `CoreScenes` scene loads, so `Update`/`OnGUI` never run and F3 does nothing.
- The game routes Unity logs nowhere useful (Player.log has no managed output). To see Unity messages and exceptions, set `[Logging.Disk] WriteUnityLog = true` and `InstantFlushing = true` temporarily.

## Dependencies

- BepInEx `6.0.0-be.697` (`BepInEx.Unity.Mono`, which brings HarmonyX) and `UnityEngine.Modules 2020.3.49` come from NuGet (`nuget.bepinex.dev`, configured in the csproj). Keep the BepInEx package version the same as the BepInEx build installed in the game. A BepInEx 5 install loads no BepInEx 6 plugins (log says `0 plugins to load`).
- Game assemblies `Mortal.Core/Battle/Combat/Story.dll` are committed in `assembly-libaries/` (note the misspelled folder name). Refresh them from `<game>\Mortal_Data\Managed` after game updates.

## Architecture

Two source files:

- `Plugin.cs` — the `BaseUnityPlugin` entry point. `Awake` binds config and calls `Harmony.CreateAndPatchAll(typeof(Patch))` (instance kept for `UnpatchSelf` in `OnDestroy`); exposes a static `Plugin.Instance` so patches can read menu state (e.g. `dice`, `diceNumber`). `Update` enforces `Time.timeScale` for accelerate mode and toggles the game's hidden developer UI objects by hard-coded hierarchy paths (`GameObject.Find("[UI]/MainUI/...")`). `OnGUI`/`DoMyWindow` draws the IMGUI menu: speed, dice control, stat editor (over `GameStatType` via `PlayerStatManagerData.Instance.Stats`), mission flag editor (`MissionManagerData.Instance.FlagsCollection`), and battle-end triggers.
- `Patch.cs` — Harmony patches against game types. Notably it forces `Debug.isDebugBuild` to return `true`, which unlocks the game's built-in dev/test features that the menu relies on; other patches then suppress the unwanted dev overlays (FPS display, profiler, `DevelopmentOnly`, test buttons).

Conventions:
- Private/internal game members are accessed via HarmonyX `Traverse` (`Traverse.Create(x).Field("_name")` / `.Method("Name", args)`), not reflection or publicizers. Patches target methods by string name, so a renamed game member compiles fine but fails at runtime. Check the BepInEx log after game updates.
- Use explicit types rather than `var` (per commit history).
