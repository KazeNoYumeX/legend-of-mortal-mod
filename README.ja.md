# 活俠傳 改造 Mod

[繁體中文](README.md) | [English](README.en.md) | 日本語 | [한국어](README.ko.md)

**活俠傳（Legend of Mortal）** 用のゲーム内チート／デバッグメニューです（BepInEx 6 プラグイン）。

> [!NOTE]
> このページはインストール方法と簡単な機能紹介のみです。各機能の詳しい説明は [English README](README.en.md) を参照してください。
> メニューは繁体字中国語のみで、日本語には対応していません。

## インストール

> [!IMPORTANT]
> この Mod は **BepInEx 6**（Unity Mono 版、`6.0.0-be.697` 推奨）専用です。**BepInEx 5 では動作しません。**
> BepInEx 5 では Mod がまったく読み込まれず、`BepInEx/LogOutput.log` に `0 plugins to load` と表示されます。
> 見分け方：`BepInEx/core` に `BepInEx.Unity.Mono.dll` があれば 6、`BepInEx.dll` だけなら 5 です。

> [!NOTE]
> 現在のバージョンはゲーム **1.0.6001.0**（タイトル画面の左下に表示）に対応しています。古いバージョンのゲームでは「劇情中隨時存檔」（ストーリー中のセーブ）と回想への書き込みが動作しません。

- [Releases](https://github.com/KazeNoYumeX/legend-of-mortal-mod/releases/latest) から `legend-of-mortal-mod.zip` をダウンロードします。BepInEx 6 be.697 が同梱され、`Type = Camera` も設定済みです。
- `legend-of-mortal-mod.zip` をゲームフォルダに展開し、同名ファイルは上書きします。以前 BepInEx 5 を入れていた場合は、先にゲームフォルダの `BepInEx/core`、`winhttp.dll`、`doorstop_config.ini` を削除してください。
- BepInEx を自分で入れる場合は、[BepInEx 6 be.697（Unity Mono x64）](https://builds.bepinex.dev/projects/bepinex_be/697/BepInEx-Unity.Mono-win-x64-6.0.0-be.697%2B5362580.zip) をダウンロードし、ゲームを一度起動して終了したあと、`BepInEx/config/BepInEx.cfg` の `[Preloader.Entrypoint]` にある `Type = Application` を `Type = Camera` に変更してください（変更しないとメニューが表示されません）。
- 骰子大師など、ほかの BepInEx 6 用 Mod と併用できます。

### アップデート

- v1.1.x 以降から：`BepInEx/plugins/LegendOfMortalMod/LegendOfMortalMod.dll` だけを差し替えます。
- v1.0.0 から、または初めてのインストール：zip 全体をゲームフォルダに展開して上書きします。

## よくある問題

- `F3` を押しても何も起きない：BepInEx 6 が入っていること、`BepInEx/config/BepInEx.cfg` が `Type = Camera` になっていることを確認してください。v1.0.0 の zip は `Application` のままだったので、書き換えるか最新版をダウンロードしてください。
- `BepInEx/LogOutput.log` が更新されない：Steam を完全に終了してから開き直し、Steam からゲームを起動してください。

## 使い方

ゲーム中に `F3` でメニューを開きます。キーは `BepInEx/config/LegendOfMortalMod.cfg` の `MenuToggleKey`、メニューの大きさは `UiScale` で変更できます。

主な機能：

- 基本：ゲーム速度、行動回数の無限化、旬のスキップ、消費回数の無限化、鍛造・煉丹・修練のポイントと銀両の無限化、戦闘の勝敗判定ボタン、戦闘の結果とイベント表示、ストーリーのフラグ表示、サイコロの結果指定、ロックされた選択肢と分岐判定のクイック解除、ピンボールの合計点変更、ストーリー中のセーブ、セーブのメモとスロット数変更、意中の人の切り替え
- 角色：能力値、好感度、技能の変更とロック
- 物品：書籍、貴重品、雑貨の所持数、鍛造・煉丹の開発項目
- 旗標：ストーリーフラグ
- 講經堂：生死簿と汗青書の解放

> [!WARNING]
> - **セーブデータを書き換えます。先にセーブデータをバックアップしてください。**
> - フラグや技能を直接変更すると、ストーリーが進まなくなったり戦闘が壊れたりすることがあります。変更前にこまめにセーブしてください。

スクリーンショットは [English README](README.en.md#screenshots) にあります。

> [!NOTE]
> この日本語ページは機械的に翻訳したものです。誤りがあれば [Issues](https://github.com/KazeNoYumeX/legend-of-mortal-mod/issues) で教えてください。
