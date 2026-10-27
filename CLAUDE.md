# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A BepInEx 6 plugin (HarmonyX-based mod) for the Unity game **活俠傳 / Legend of Mortal** (Unity 2020.3.49, Mono). It adds an in-game IMGUI cheat/debug menu (toggled with `F3`, configurable via BepInEx config key `LegendOfMortalMod.MenuToggleKey`). UI strings and many game identifiers (e.g. `GameStatType.體力`) are Traditional Chinese — keep them as-is.

## Build

- SDK-style `LegendOfMortalMod.csproj`, `net472`, `LangVersion latest`: `dotnet build -c Release`
- After building, the DLL is copied to `$(GamePath)\BepInEx\plugins\LegendOfMortalMod\` if that folder exists. `GamePath` defaults to the Steam install (`C:\Program Files (x86)\Steam\steamapps\common\LegendOfMortal`); pass `-p:GamePath=NUL` to build without deploying.
- Formatting: CSharpier 1.x — `dotnet tool restore` then `dotnet csharpier format .` (`check .` to verify). Line width and indent come from `.editorconfig` (120 columns for `.cs`); it also formats the csproj.
- No tests. Verify by launching with `explorer.exe "steam://rungameid/1859910"` and reading `<game>\BepInEx\LogOutput.log`. It should list `Loading [活俠傳修改選單 <version>]` and no Harmony errors. Check the log's timestamp first: when BepInEx doesn't start, the file is not rewritten and you read the previous run's log.

## Game/loader gotchas

- Never start `Mortal.exe` directly. The game restarts itself through Steam, and if Steam wasn't running it inherits the injected game's `DOORSTOP_INITIALIZED` environment variable. Every later Steam launch then skips Doorstop and BepInEx never starts (log not rewritten, `winhttp.dll` still loaded). Fix: fully exit Steam and reopen it normally.
- `BepInEx.cfg` `[Preloader.Entrypoint] Type` must be `Camera`, not the default `Application`. With `Application` the chainloader runs before the engine is ready (`Screen.width` reports ~298) and the plugin is destroyed when the `CoreScenes` scene loads, so `Update`/`OnGUI` never run and F3 does nothing.
- The game routes Unity logs nowhere useful (Player.log has no managed output). To see Unity messages and exceptions, set `[Logging.Disk] WriteUnityLog = true` and `InstantFlushing = true` temporarily.

## Dependencies

- BepInEx `6.0.0-be.697` (`BepInEx.Unity.Mono`, which brings HarmonyX) and `UnityEngine.Modules 2020.3.49` come from NuGet (`nuget.bepinex.dev`, configured in the csproj). Keep the BepInEx package version the same as the BepInEx build installed in the game. A BepInEx 5 install loads no BepInEx 6 plugins (log says `0 plugins to load`).
- Game assemblies `Mortal.Core/Battle/Combat/Free/Story.dll`, `UnityEngine.UI.dll` and `Fungus.dll` are committed in `lib/`. Refresh them from `<game>\Mortal_Data\Managed` after game updates.

## Architecture

Source files (`Plugin.cs` and `Patch.cs` at the root, one file per feature under `Features/` in namespace `LegendOfMortalMod.Features`; the game now has its own `Mortal.Core.StorySave`, so files that use ours alias it):

- `Plugin.cs` — the `BaseUnityPlugin` entry point. `Awake` binds config and calls `Harmony.CreateAndPatchAll(typeof(Patch))` (instance kept for `UnpatchSelf` in `OnDestroy`); exposes a static `Plugin.Instance` so patches can read menu state (e.g. `unlimitedSpend`). `Update` enforces `Time.timeScale` for accelerate mode and toggles the game's hidden developer UI objects by hard-coded hierarchy paths (`GameObject.Find("[UI]/MainUI/...")`). `OnGUI`/`DoMyWindow` draws the tabbed IMGUI menu (an invisible uGUI panel under the window keeps clicks from reaching the game UI; the 選單不透明 toggle, config `BlockMenuClicks`, turns it off; 選單最小化 hides the tab bar and shrinks the window to the 基本 tab, without its set-and-forget toggles; the battle/story info below stays, at a fixed height): 基本 (speed, unlimited spend, points/money and action points, 跳過本旬, toggles for the battle and dice overlays and 劇情中隨時存檔 (config `StorySaveAnywhere`), a 切換心上人 picker for 愛人), then 角色屬性 (`PlayerStatManagerData.Instance.Stats`), 好感度 (`.Relationships`), 旗標 (`MissionManagerData.Instance.FlagsCollection`), 書籍 (`ItemDatabase.Instance.Books`), 貴重品 (`.Special`), 雜物 (`.Miscs`), 技能 (`PlayerStatManagerData.Instance.Talents`), 講經堂 (`LibrarySystem.Instance.Dead` / `.EndGame`), which share one searchable table (`DrawValueRow`) with set/+1/-1 and a lock that `Update` re-applies every frame. 講經堂 rows are unlock only and live in the global save: `Update` writes it with `SaveSystem.SaveUniverseData` once the game has loaded it, so an early edit never overwrites it with empty records.
- `Features/BattleResult.cs` — semi-transparent 判定勝利/落敗/平手 IMGUI buttons (second click within 3 s confirms) over a fight (單挑 always, 團體戰 only while paused), ending it through the game's own result paths.
- `Features/BattleInfo.cs` — 顯示戰鬥結果與事件: text lines for the 基本 tab (drawn small below its rows by `Plugin.DrawInfo`, only while fighting). 單挑: `CombatLevel._winResult` / `_loseResult` / `_conditionResult` and its `Events` condition/action pairs, built once per level. 團體戰: `GameLevelManager._resultFlag`, `BattleLevel._events` triggers (second, team count, condition) and actions, rebuilt each Layout.
- `Features/GameText.cs` — shared text for flags (`GameText.Flag`) and `StatCompareItem` conditions, used by BattleInfo and StoryFlags.
- `Features/DiceControl.cs` — uGUI 指定 buttons injected into the dice dialog, one per option; works out the raw roll that lands on it and forces the result in the `CheckPointManager.Dice` patches; that roll skips the wheel spin and the 繼續 prompt.
- `Features/RoundControl.cs` — 基本 tab's 行動點無限 (a `GameStat.AddValue` patch blocks spending 行動次數) and 跳過本旬 (free map only: sets the points down one at a time, running the mission check after each as the game does, so a story due partway still plays).
- `Features/SaveNotes.cs` — 存檔備註: `SaveSlotPanel`/`LoadSlotPanel.SetupText` postfixes add a uGUI `InputField` (built in code, styled from the slot's time `Text`) under each manual slot's time; `onEndEdit` stores the note in `SlotData`, and `SaveSystem.DeleteSaveData` clears the slot's data.
- `Features/SlotData.cs` — small per-slot values (save notes, a story save's background and music) in `BepInEx\config\LegendOfMortalMod.slots.txt`, never in the game's saves.
- `Features/StoryFlags.cs` — 顯示本段劇情旗標. As each story script loads it regex-scans the Lua for `statmodifymanager.SetFlag/AddFlag` and `checkpointmanager.Switch/Condition`, and `Lines()` gives the 基本 tab those flags and each check's `ConditionResultItem` conditions marked by their current `Check()`; `CheckPointManager.Switch`/`Condition` prefixes start recording the results the game itself rolls (a `ConditionResultItem.Check` postfix; conditions can be random, so checking again would differ) and their postfixes write them with the result to the 回想 with `StoryLogData.AddText(text, false)` (not saved).
- `Features/StorySave.cs` — 劇情中隨時存檔. A save keeps only the script to start from, so as each story script loads (`LuaEnvironment.LoadLuaFunction`) a `CreateSaveData` copy is kept; a story save away from the game's own save points (`LuaManager.ToggleSaveButton`) writes that copy, and a load replays the script with the values it started with. Scripts often inherit the background, music and ambient sound from the script before, so the snapshot also notes `FlowchartPlaceholder.view`'s `ViewName` and `SoundManager._currentMusic` / `_currentEnvSound`, kept per slot in `SlotData`, and the replayed script gets the Lua that sets them prepended. Right after a load into a story with no action left, one `UpdateCheckMissions` is skipped so the 旬 doesn't change under the story.
- `Patch.cs` — Harmony patches against game types. `GameStat.AddValue` blocks spending 行動次數, 銀兩 and the points of the 修練/鍛造/煉丹 panels while their toggles are on; the `MartialPanel`/`UpgradePanel` patches give 無限消費 (the martial panel's own unlimited mode, the upgrade panel's count kept topped up). It does not force `Debug.isDebugBuild`, so the game hides its own dev/test UI (`DevelopmentOnly`, test win/lose buttons, FPS display) as in a release build; only the profiler overlay needs a patch.

Conventions:
- Private/internal game members are accessed via HarmonyX `Traverse` (`Traverse.Create(x).Field("_name")` / `.Method("Name", args)`), not reflection or publicizers. Patches target methods by string name, so a renamed game member compiles fine but fails at runtime. Check the BepInEx log after game updates.
- Use explicit types rather than `var` (per commit history).
- `CHANGELOG.md` follows Keep a Changelog: add each user-facing change under `[Unreleased]` in the same commit, in Traditional Chinese under the English `Added` / `Changed` / `Removed` / `Fixed` headings. A release turns `[Unreleased]` into `[x.y.z] - YYYY-MM-DD`, adds its compare link, and its GitHub release notes are taken from that section.
