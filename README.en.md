# Legend of Mortal Mod

[繁體中文](README.md) | English | [日本語](README.ja.md) | [한국어](README.ko.md)

An in-game cheat/debug menu for **Legend of Mortal (活俠傳)**, built as a BepInEx 6 plugin.

> [!NOTE]
> The menu itself is in Traditional Chinese only. The tables below explain what each part does.

## Installation

> [!IMPORTANT]
> This mod only supports **BepInEx 6** (Unity Mono, `6.0.0-be.697` recommended). **BepInEx 5 is not supported.**
> With BepInEx 5 the mod does not load at all, and `BepInEx/LogOutput.log` shows `0 plugins to load`.
> To tell them apart: BepInEx 6 has `BepInEx.Unity.Mono.dll` in `BepInEx/core`; BepInEx 5 only has `BepInEx.dll`.

> [!NOTE]
> This version targets game version **1.0.6001.0** (shown at the bottom left of the title screen). On older game versions, save anywhere in a story and writing to the 回想 (backlog) do not work.

- Download `legend-of-mortal-mod.zip` from [Releases](https://github.com/KazeNoYumeX/legend-of-mortal-mod/releases/latest). It includes BepInEx 6 be.697, already set to `Type = Camera`.
- Extract `legend-of-mortal-mod.zip` into the game folder and overwrite existing files. If BepInEx 5 was installed before, first delete `BepInEx/core`, `winhttp.dll` and `doorstop_config.ini` from the game folder.
- To install BepInEx yourself, download [BepInEx 6 be.697 (Unity Mono x64)](https://builds.bepinex.dev/projects/bepinex_be/697/BepInEx-Unity.Mono-win-x64-6.0.0-be.697%2B5362580.zip), start the game once and close it, then open `BepInEx/config/BepInEx.cfg` and change `Type = Application` under `[Preloader.Entrypoint]` to `Type = Camera` (without this the menu never appears).
- Works alongside other BepInEx 6 mods, such as 骰子大師.

### Upgrade

- From v1.1.x or later: only replace `BepInEx/plugins/LegendOfMortalMod/LegendOfMortalMod.dll`.
- From v1.0.0, or a first install: extract the whole zip into the game folder and overwrite.

## Troubleshooting

- `F3` does nothing: make sure BepInEx 6 is installed and `BepInEx/config/BepInEx.cfg` says `Type = Camera`. The v1.0.0 zip shipped with `Application`; change it or download the latest release.
- `BepInEx/LogOutput.log` is not updated: fully exit Steam, reopen it, then launch the game from Steam.

## Usage

Press `F3` in game to open the menu. The key can be changed with `MenuToggleKey` in `BepInEx/config/LegendOfMortalMod.cfg`, and the menu size with `UiScale`.

The top row holds the groups; 角色 (Character) and 物品 (Items) have a second row of tabs:

| Tab | Meaning | What it does |
|---|---|---|
| 基本 | Basic | Game speed, unlimited action points, skip this round, unlimited sessions, unlimited forging/alchemy/training points and money, battle result buttons, battle results and events, story flags, dice pick buttons, quick unlock for events, options and branch checks, pinball total, save anywhere in a story, save notes and slot count, lover switch |
| 角色／角色屬性 | Stats | All player stats: search, set, +1 / -1, lock. In sections: 常用 (common: 學問, 嘴力, 抗毒, 抗麻, 性情, 處世, 道德, 修養 and so on, in the order of the game's stats page), 武學 (martial), 資源 (resources), 門派 (sect), 其他 (other) |
| 角色／好感度 | Affection | Relationship values for every character, same controls; 全滿 (max) / 歸零 (reset) set every character the search shows to 100 / 0; click the note 已結識 (met) / 未結識 (not met) to toggle it, and 全部結識 (meet all) marks every character the search shows as met |
| 角色／技能 | Skills | Level of each of the hero's skills (0 = not learned), same controls; the note marks duel (決鬥) and group battle (戰役) skills |
| 物品／書籍 | Books | How many of each book and martial arts manual you hold, same controls; 全持有 (own all) gives 1 of every shown book you lack, 歸零 (reset) sets shown books to 0 but skips manuals you have trained, since the save drops a book you hold none of and its training progress with it |
| 物品／貴重品 | Valuables | How many of each valuable you hold, same controls; 全持有 (own all) gives 1 of every shown valuable you lack. Most are story items, so owning them all may change how the story goes |
| 物品／雜物 | Misc items | How many of each misc item you hold, same controls; 全持有 (own all) gives 1 of every shown misc item you lack |
| 物品／開發項目 | Development | Every 鍛造 (forging) and 煉丹 (alchemy) item: weapon, armor, the three hidden weapons, poison and pill. 目前 is its level (-1 = not developed). 裝備 (equip) puts it in its slot right away, without following the upgrade path; an undeveloped item starts at level 0. Setting the level also applies what each level gained unlocks |
| 旗標 | Flags | Story flags, same controls |
| 講經堂 | Library | Every entry of 生死簿 (deaths) and 汗青書 (endings). 目前 is how many times it was reached (0 = locked); a locked entry has a 解鎖 (unlock) button. Search 生死簿 or 汗青書 to see one list; 全部解鎖 (unlock all, press again within 3 s to confirm) unlocks every locked entry the search shows. Entries live in the global save shared by all saves, so the current save is untouched, and the menu cannot lock them again |

Options on the 基本 (Basic) tab:

| Label | Meaning |
|---|---|
| 遊戲加速 | Speed up the game by the multiplier in the field (2 by default) |
| 行動點無限 | Fill the action points when switched on; nothing spends them until it is switched off |
| 跳過本旬 | Map only; click again within 3 seconds to confirm. Gives up the round's remaining action points and moves to the next round; a story due on the way (some main missions need 2 or 1 points left) stops there and plays |
| 無限消費 | No limit on the 最大消費 (max sessions) count of training (修練), alchemy (煉丹) and forging (鍛造); shown as ∞. Takes effect at once even with the panel open; unticking waits until the panel is opened again |
| 無限鍛造/煉丹/修練點數/銀兩 | Sets money and the points to their maximum when ticked; after that, spending money (shops, forging, story alike) and training, alchemy and forging take nothing. A panel not opened yet has its points filled the first time it opens. Money or points locked on the stats tab keep the locked value; unlock them first |
| 以下全部開啟 | Turn all on: tick or untick the seven options below at once (the ones the minimized menu hides) |
| 顯示戰鬥判定按鈕 | Show 判定勝利 (win) / 判定落敗 (lose) at the top of a fight, plus 判定平手 (draw) in duels that have a draw exit. Click again within 3 s to confirm; the story continues as if the fight ended normally. Always shown in duels (單挑), only in the pause menu in group battles (團體戰) |
| 顯示戰鬥結果與事件 | Battle results and events: during a fight, small text below the basic tab lists which flag a win or a loss sets and to what, and the events the level can trigger with their effects (dialogue, stat changes, support, a story switch, ending the fight, …). Duels (單挑) show the current round, with events by round, HP, status and so on; group battles (團體戰) show the elapsed seconds and both sides' headcount, with events by second, headcount left and conditions, and mark the ones already fired 已觸發. In a duel, 我方血量 / 敵方血量 (your / enemy HP) rows above it set either side's HP (1 to max), handy for events that fire below some HP |
| 顯示本段劇情旗標（判定寫進回想） | Story flags (checks written to the backlog): in a story, small text below the basic tab lists the flags this script sets (with their current value) and the conditions of each branch check, marked ✔ true / ✘ false by the current values. When the game makes a check, it also writes each branch's result (— for branches it never reached) and the branch taken to the game's 回想 (backlog) in small gold text; these entries are not saved and clear on load |
| 骰子畫面顯示指定按鈕 | Add a 指定 (pick) button left of each option of a story dice roll (check); it rolls that result straight away, skipping the wheel. Picking again after a roll costs a fate point, like the game's reroll |
| 快速解鎖事件/選項/分支判定 | Quick unlock for events, options and branch checks. A locked option (「解鎖條件：？？？」, unlock condition) whose condition can be met by changing values gets a 快速解鎖 (quick unlock) button: it sets the unmet values so the condition holds, the option unlocks on the spot, and choosing it runs the game's own branch. When the condition compares a sum (e.g. the six 道 counters adding up to more than 1), the gap goes on one of them. Menus like 議題 and 走訪 hide unmet options entirely; a 「另有 N 個選項未滿足條件」 (N more options not met) button beside the menu title lists them with their conditions and the values quick unlock would change, each with its own 快速解鎖. After changing values in the menu, clicking that button shows the options whose condition now holds. With 顯示本段劇情旗標 on, each branch check that values can satisfy gets a 設定 (set) button that makes just that branch hold, ignoring the branches before it. What was changed (e.g. 「修養 3→5」) is written to the 回想 (backlog); if the condition still fails (e.g. past a stat's cap), the values are put back |
| 彈珠台可改總分 | Pinball total: a 「總分改為：[box] 套用」 (set total to) row under the pinball total. Type any score and press Enter or 套用 (apply); only the total changes. The story after the table only checks which band the total falls in (100+, 60+, 30+ or lower), so leave with the game's own button afterwards. Typing does not launch the ball |
| 劇情中隨時存檔 | Save anywhere in a story: the menu's 存檔 (save) works mid-story, e.g. before a choice. Loading replays this part of the story from its start with the values it started with, so after a short replay you are back at the same choice; the background, music and ambient sound are restored as they were when you saved. The game's own save points, the map and battles are unchanged. It is remembered. **It writes save files; back up your saves first** |
| 存檔備註與格數 | Save notes and slot count, off by default and remembered. 趙活系統 (ZhaoHuoSystem) has its own save notes and slot count; with both on, each slot shows two sets of notes and the slots are rebuilt twice, so leave this off if you use it. The load and save pages get the number of manual slots in the field after it (20–99, default 40; the game has 20), from the next time you open them; more slots means more save files read when a page opens, so it is a little slower. Each slot with a save gets a 備註 (note) button at the right end of its save time row for a note of up to 20 characters, shown on the line under the save time and cut short with … when it does not fit; notes live in `BepInEx/config/LegendOfMortalMod.slots.txt`, not in the game's saves, and go with a deleted save. Switched off, the game's 20 slots come back without notes; the extra saves and notes are kept and show again when you switch it back on |
| 選單不透明 | Clicks on the menu don't reach the game buttons behind it (on by default, remembered). It only blocks clicks; the menu looks the same |
| 選單最小化 | Minimize: hide the other tabs and shrink the window to the basic tab's everyday options (無限消費, 無限鍛造/煉丹/修練點數/銀兩, the seven options under 以下全部開啟, 存檔備註與格數 and 選單不透明 are hidden, but stay in effect); battle results and events and story flags still show below. Untick it to restore |
| 切換心上人 | Lover: a dropdown showing the current lover; open it and pick one to switch (the same as 愛人 on the stats tab). If 愛人 is locked on the stats tab it switches back; unlock it first |

Table columns: 名稱 Name, 備註 Note, 範圍 Range, 目前 Current, 新數值 New value; buttons 設定 Set and 鎖定 Lock (講經堂 has only 解鎖 Unlock). A locked value is restored every frame. Setting a locked value (including 全滿 / 歸零) re-locks it at the new value, which is handy for things like money (銀兩) or poison points (毒藥點數). Every tab has a search box that matches the name or the note.

> [!WARNING]
> - Locking story flags can soft-lock the story. If unsure, don't lock them.
> - Normal play never teaches many skills at once, and some skills replace a lower one. Setting levels directly can leave you with both, which may break battles.
> - Changing whether a character is met only adds them to the list. It does not play the meeting story or set its flags, so related events may break or happen early.
> - Flags and skills deliberately have no one-click buttons like 全持有 / 歸零, because they are riskier for your save. Save often before editing.

See [CHANGELOG.md](CHANGELOG.md) (Traditional Chinese) for what changed in each version.

## Screenshots

### 基本 Basic

Every option; the minimized menu keeps only game speed, unlimited action points, skip this round, 選單最小化 and the lover switch.

![Basic tab](docs/screenshots/basic.webp)

### 劇情旗標 Story flags

The minimized menu in a story, showing this script's branch check: each branch's conditions with the current values; ✘ means false right now.

![Story flags](docs/screenshots/story-flags.webp)

### 回想 Story log

When the game makes a branch check, the 回想 log notes the branch, each condition's result and the branch taken, in small gold text.

![Branch check in the story log](docs/screenshots/story-log.webp)

### 戰鬥結果與事件 Battle info (duel)

The minimized menu in a duel: both sides' HP (which you can set), the current round, the flags a win or a loss sets, and which event fires on which condition.

![Battle info in a duel](docs/screenshots/battle-combat.webp)

### 戰鬥結果與事件 Battle info (group battle)

In a group battle: elapsed seconds, both sides' headcount, the time limit and the flags the result is written to.

![Battle info in a group battle](docs/screenshots/battle-group.webp)

### 角色屬性 Stats

In sections, the common stats first.

![Stats tab](docs/screenshots/stats.webp)

### 好感度 Affection

![Affection tab](docs/screenshots/affection.webp)

### 書籍 Books

![Books tab](docs/screenshots/books.webp)

### 旗標 Flags

![Flags tab](docs/screenshots/flags.webp)

### 技能 Skills

![Skills tab](docs/screenshots/talents.webp)

### 選單最小化 Minimized menu

![Minimized menu](docs/screenshots/minimized.webp)
