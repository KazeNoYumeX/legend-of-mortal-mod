# 活俠傳修改 Mod 

## 安裝方法 Installation

- `legend-of-mortal-mod.zip` 中包含 [BepInEx](https://github.com/BepInEx/BepInEx)
- 將 `legend-of-mortal-mod.zip` 解壓縮到遊戲目錄下
- 啟動一次遊戲後關閉，打開 `BepInEx/config/BepInEx.cfg`，把 `[Preloader.Entrypoint]` 底下的 `Type = Application` 改成 `Type = Camera`（不改的話選單不會出現）

## 疑難排解 Troubleshooting

- 按 `F3` 沒反應：確認上面的 `Type = Camera` 已修改
- `BepInEx/LogOutput.log` 沒有更新：完全結束 Steam 後重新開啟，再從 Steam 啟動遊戲

## 使用方法 Usage

- 遊戲中點擊 `F3` 開啟選單
