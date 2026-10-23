using System;
using HarmonyLib;
using Mortal.Core;
using Mortal.Free;
using Mortal.Story;
using UnityEngine.UI;

namespace LegendOfMortalMod
{
    // 劇情中隨時存檔。遊戲存檔只記從哪個劇情腳本開始，讀檔時從腳本第一行重播；在腳本中間存當下的數值，重播會把
    // 已經套用的增減再套一次。所以每個腳本開始時先建好一份存檔，劇情中按存檔時寫入這份：讀檔從這個腳本開頭、
    // 用當時的數值重播，玩家重看一小段後回到同一個選項。遊戲自己的存檔點、地圖、戰鬥照舊。
    // 改寫自 Toufff/zhaohuo-system 的 SegmentSaves，Copyright (c) 2026 Toufff，MIT 授權，見 THIRD-PARTY-NOTICES.md
    internal static class StorySave
    {
        private const string ScriptExtension = ".LuaScript";
        public static bool Enabled;
        private static GameSave _snapshot;

        // A script turned saving on itself: that is one of the game's own save points, which keep the game's own save
        private static bool _nativeOpen;
        private static bool _writing;
        private static bool _roundCheck;

        // Each Lua chunk as the game loads it, before any of it runs. The copy is taken even while switched off, so
        // switching on mid-story works at once
        public static void ScriptLoaded(string friendlyName)
        {
            string name = friendlyName ?? "";
            name = name.Substring(name.LastIndexOfAny(new[] { '/', '\\' }) + 1);
            // Inline chunks (tmp…) run inside a script and keep its copy
            if (!name.EndsWith(ScriptExtension, StringComparison.OrdinalIgnoreCase))
                return;
            name = name.Substring(0, name.Length - ScriptExtension.Length);
            if (name.Length == 0 || name.StartsWith("tmp", StringComparison.OrdinalIgnoreCase))
                return;
            _snapshot = null;
            PlayerStatManagerData player = PlayerStatManagerData.Instance;
            if (StoryManager.Instance == null || SaveSystem.Instance == null || player == null)
                return;
            // Only a script the story itself moved to; it is what a save starts from
            if (!string.Equals(player.CurrentStoryScript, name, StringComparison.OrdinalIgnoreCase))
                return;
            _snapshot = Traverse.Create(SaveSystem.Instance).Method("CreateSaveData").GetValue<GameSave>();
            if (_snapshot == null)
                return;
            _snapshot.StartStoryScript = player.CurrentStoryScript;
            Plugin.Log.LogInfo($"劇情中存檔：已記下 {name} 開始時的存檔");
        }

        public static void NativeToggle(bool active) => _nativeOpen = active;

        // Keeps the story menu's 存檔 on while this script's copy is at hand
        public static void Update()
        {
            StoryManager story = StoryManager.Instance;
            if (!Enabled || _snapshot == null || _nativeOpen || story == null)
                return;
            Button save = Traverse.Create(story).Field("_menuPanelController").Field("_saveButton").GetValue<Button>();
            if (save != null && !save.interactable)
                story.ToggleSaveButton(true);
        }

        // Before SaveSystem.SaveGameData(slot): in a story, away from the game's own save points, write the copy
        public static void BeforeSave()
        {
            _writing =
                Enabled
                && _snapshot != null
                && !_nativeOpen
                && StoryManager.Instance != null
                && FreeManager.Instance == null;
            if (_writing)
                _snapshot.TimeTick = DateTime.Now.Ticks;
        }

        public static void AfterSave(string slot)
        {
            if (_writing)
                Plugin.Log.LogInfo($"劇情中存檔：{slot} 從 {_snapshot.StartStoryScript} 開始");
            _writing = false;
        }

        // In place of SaveSystem.CreateSaveData while that save is written
        public static GameSave Substitute() => _writing ? _snapshot : null;

        public static void Loaded() => _roundCheck = true;

        public static void SceneChanged()
        {
            _nativeOpen = false;
            _roundCheck = false;
        }

        // A load runs the mission check before entering the save's scene, and with no action left that check starts
        // the next 旬: a story entered with the last action, saved here, then loads into the next 旬's start script
        // and waits on 讀取中 for good. Right after such a load the check is skipped once; back on the map the game
        // runs it itself.
        public static bool KeepRound()
        {
            if (!_roundCheck)
                return false;
            _roundCheck = false;
            PlayerStatManagerData player = PlayerStatManagerData.Instance;
            if (
                player == null
                || player.CurrentScene != "Story"
                || player.CurrentActionCount != 0
                || string.IsNullOrEmpty(player.CurrentStoryScript)
            )
                return false;
            Plugin.Log.LogInfo($"讀檔：行動點為 0，先不換旬，從 {player.CurrentStoryScript} 開始");
            return true;
        }
    }
}
