# Legend of Mortal Mod

[繁體中文](README.md) | English

An in-game cheat/debug menu for **Legend of Mortal (活俠傳)**, built as a BepInEx 6 plugin.

> [!NOTE]
> The menu itself is in Traditional Chinese only. The tables below explain what each part does.

## Installation

> [!IMPORTANT]
> This mod only supports **BepInEx 6** (Unity Mono, `6.0.0-be.697` recommended). **BepInEx 5 is not supported.**
> With BepInEx 5 the mod does not load at all, and `BepInEx/LogOutput.log` shows `0 plugins to load`.
> To tell them apart: BepInEx 6 has `BepInEx.Unity.Mono.dll` in `BepInEx/core`; BepInEx 5 only has `BepInEx.dll`.

- Download `legend-of-mortal-mod.zip` from [Releases](https://github.com/KazeNoYumeX/legend-of-mortal-mod/releases/latest). It includes BepInEx 6 be.697, already set to `Type = Camera`.
- Extract `legend-of-mortal-mod.zip` into the game folder and overwrite existing files. If BepInEx 5 was installed before, first delete `BepInEx/core`, `winhttp.dll` and `doorstop_config.ini` from the game folder.
- To install BepInEx yourself, download [BepInEx 6 be.697 (Unity Mono x64)](https://builds.bepinex.dev/projects/bepinex_be/697/BepInEx-Unity.Mono-win-x64-6.0.0-be.697%2B5362580.zip), start the game once and close it, then open `BepInEx/config/BepInEx.cfg` and change `Type = Application` under `[Preloader.Entrypoint]` to `Type = Camera` (without this the menu never appears).

## Troubleshooting

- `F3` does nothing: make sure BepInEx 6 is installed and `BepInEx/config/BepInEx.cfg` says `Type = Camera`. The v1.0.0 zip shipped with `Application`; change it or download the latest release.
- `BepInEx/LogOutput.log` is not updated: fully exit Steam, reopen it, then launch the game from Steam.

## Usage

Press `F3` in game to open the menu. The key can be changed with `MenuToggleKey` in `BepInEx/config/LegendOfMortalMod.cfg`, and the menu size with `UiScale`.

| Tab | Meaning | What it does |
|---|---|---|
| 基本 | Basic | Game speed, dice control, per-session spend limit, battle-end buttons |
| 角色屬性 | Stats | All player stats: search, set, +1 / -1, lock |
| 好感度 | Affection | Relationship values for every character, same controls; 全滿 (max) / 歸零 (reset) set every character the search shows to 100 / 0 |
| 旗標 | Flags | Story flags, same controls |
| 書籍 | Books | How many of each book and martial arts manual you hold, same controls; 全持有 (own all) gives 1 of every shown book you lack, 歸零 (reset) sets shown books to 0 but skips manuals you have trained, since the save drops a book you hold none of and its training progress with it |

Options on the 基本 (Basic) tab:

| Label | Meaning |
|---|---|
| 開啟加速 | Speed up the game by the multiplier in the field |
| 控制骰子 | Force story dice rolls (checks) to the number in the field (1–99) |
| 單次消費上限 | Override the per-session spend limit of training (練功), alchemy (煉丹) and forging (鍛造) |
| 顯示直接勝利/失敗按鈕 | Duel (單挑): show instant win/lose buttons |
| 玩家死亡 / 友方獲勝 / 敵方獲勝 / 時間到 | Group battle (團體戰): end with player dies / allies win / enemies win / time out |

Table columns: 名稱 Name, 備註 Note, 範圍 Range, 目前 Current, 新數值 New value; buttons 設定 Set and 鎖定 Lock. A locked value is restored every frame. Setting a locked value (including 全滿 / 歸零) re-locks it at the new value.

> [!WARNING]
> Locking story flags can soft-lock the story. Use with care.
