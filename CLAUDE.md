# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A BepInEx plugin (Harmony-based mod) for the Unity game **活俠傳 / Legend of Mortal**. It adds an in-game IMGUI cheat/debug menu (toggled with `F3`, configurable via BepInEx config key `LegendOfMortalMod.MenuToggleKey`). UI strings and many game identifiers (e.g. `GameStatType.體力`) are Traditional Chinese — keep them as-is.

## Build

- Old-style (non-SDK) `LegendOfMortalMod.csproj`, targets .NET Framework 4.8.1. Build with MSBuild or Rider:
  `msbuild LegendOfMortalMod.sln /p:Configuration=Release`, or `dotnet build LegendOfMortalMod.csproj -c Release` (works without Visual Studio). Pass `-p:OutputPath=<dir>\` to compile-check without overwriting the installed plugin.
- Output goes straight into the game's plugin folder: `..\..\..\Games\LegendOfMortal v1.0.3200.2\BepInEx\plugins\` (relative to the project dir). Adjust `OutputPath` if the game lives elsewhere.
- Note: `*.csproj` and `*.sln` are matched by `.gitignore` (Unity template), so edits to them are not tracked unless force-added.
- Formatting: CSharpier is the configured local tool — `dotnet tool restore` then `dotnet csharpier .`
- No tests. Verification means building, launching the game, and checking `BepInEx/LogOutput.log`.

## Dependencies

All references are prebuilt DLLs in `assembly-libaries/` (note the misspelled folder name), all with `Private=False` so nothing is copied to output:
- Game assemblies extracted from the game's `Managed` folder: `Mortal.Core`, `Mortal.Battle`, `Mortal.Combat`, `Mortal.Story`, plus `UnityEngine.*` modules.
- BepInEx 6 (`BepInEx.Core`, `BepInEx.Unity.Mono` — plugin uses `BepInEx.Unity.Mono.BaseUnityPlugin`) and HarmonyX (`0Harmony`).

When a game update changes APIs, refresh the `Mortal.*.dll` files from the game install.

## Architecture

Two source files:

- `Plugin.cs` — the `BaseUnityPlugin` entry point. `Awake` binds config and calls `Harmony.CreateAndPatchAll(typeof(Patch))`; exposes a static `Plugin.Instance` so patches can read menu state (e.g. `dice`, `diceNumber`). `Update` enforces `Time.timeScale` for accelerate mode and toggles the game's hidden developer UI objects by hard-coded hierarchy paths (`GameObject.Find("[UI]/MainUI/...")`). `OnGUI`/`DoMyWindow` draws the IMGUI menu: speed, dice control, stat editor (over `GameStatType` via `PlayerStatManagerData.Instance.Stats`), mission flag editor (`MissionManagerData.Instance.FlagsCollection`), and battle-end triggers.
- `Patch.cs` — Harmony patches against game types. Notably it forces `Debug.isDebugBuild` to return `true`, which unlocks the game's built-in dev/test features that the menu relies on; other patches then suppress the unwanted dev overlays (FPS display, profiler, `DevelopmentOnly`, test buttons).

Conventions:
- Private/internal game members are accessed via HarmonyX `Traverse` (`Traverse.Create(x).Field("_name")` / `.Method("Name", args)`), not reflection or publicizers. Patches target methods by string name, so a renamed game member compiles fine but fails at runtime. Check the BepInEx log after game updates.
- Use explicit types rather than `var` (per recent commit history).
