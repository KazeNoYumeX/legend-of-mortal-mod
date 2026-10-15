# 活俠傳修改 Mod 

## 安裝方法 Installation

> [!IMPORTANT]
> 本 Mod 只支援 **BepInEx 6**（Unity Mono 版，建議 `6.0.0-be.697`），**不支援 BepInEx 5**。
> 裝成 BepInEx 5 時 Mod 完全不會載入，`BepInEx/LogOutput.log` 會顯示 `0 plugins to load`。
> 判斷方式：`BepInEx/core` 裡有 `BepInEx.Unity.Mono.dll` 是 6，只有 `BepInEx.dll` 是 5。

- 從 [Releases](https://github.com/KazeNoYumeX/legend-of-mortal-mod/releases/latest) 下載 `legend-of-mortal-mod.zip`，內含 BepInEx 6 be.697，且已設定好 `Type = Camera`
- 將 `legend-of-mortal-mod.zip` 解壓縮到遊戲目錄下，遇到同名檔案請選擇覆蓋（若已裝過 BepInEx 5，請先刪除遊戲目錄下的 `BepInEx/core`、`winhttp.dll`、`doorstop_config.ini`）
- 自行安裝 BepInEx 的話，請下載 [BepInEx 6 be.697（Unity Mono x64）](https://builds.bepinex.dev/projects/bepinex_be/697/BepInEx-Unity.Mono-win-x64-6.0.0-be.697%2B5362580.zip)，啟動一次遊戲後關閉，打開 `BepInEx/config/BepInEx.cfg`，把 `[Preloader.Entrypoint]` 底下的 `Type = Application` 改成 `Type = Camera`（不改的話選單不會出現）

## 疑難排解 Troubleshooting

- 按 `F3` 沒反應：確認裝的是 BepInEx 6，且 `BepInEx/config/BepInEx.cfg` 裡是 `Type = Camera`（v1.0.0 的壓縮檔是 `Application`，請改掉或重新下載最新版）
- `BepInEx/LogOutput.log` 沒有更新：完全結束 Steam 後重新開啟，再從 Steam 啟動遊戲

## 使用方法 Usage

- 遊戲中點擊 `F3` 開啟選單
