using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Mortal.Core;
using UnityEngine;
using UnityEngine.UI;

namespace LegendOfMortalMod.Features
{
    // 存檔備註：選單的「存檔備註」頁替每個存檔欄位寫一行字，遊戲的存檔、讀檔畫面接在標題後面顯示。
    // 備註跟著欄位走（SlotData），同一格重複存檔不會清掉，刪除存檔時一起刪
    internal static class SaveNotes
    {
        private const string Note = "note";
        private const float RefreshTime = 2f;
        private static List<(string slot, DateTime time)> _slots = new List<(string, DateTime)>();
        private static float _refreshAt;
        private static Vector2 _scroll;

        // 存檔欄位是數字（Save_018.dat）；Save_universe 之類的全域存檔不列
        private static List<(string slot, DateTime time)> FindSlots()
        {
            string file = Traverse.Create(SaveSystem.Instance).Method("GetSaveDataFile", "0").GetValue<string>();
            string folder = file == null ? null : Path.GetDirectoryName(file);
            if (folder == null || !Directory.Exists(folder))
                return new List<(string, DateTime)>();
            return Directory
                .GetFiles(folder, "Save_*.dat")
                .Select(path =>
                    (slot: Path.GetFileNameWithoutExtension(path).Substring(5), time: File.GetLastWriteTime(path))
                )
                .Where(item => item.slot.Length > 0 && item.slot.All(char.IsDigit))
                .OrderBy(item => item.slot)
                .ToList();
        }

        // 選單頁面；欄位清單只在 Layout 時重讀，免得 Layout 和 Repaint 畫的列數不同
        public static void Draw()
        {
            if (SaveSystem.Instance == null)
            {
                GUILayout.Label("進入遊戲後才能編輯存檔備註");
                return;
            }
            if (Event.current.type == EventType.Layout && Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + RefreshTime;
                _slots = FindSlots();
            }
            if (_slots.Count == 0)
            {
                GUILayout.Label("還沒有存檔");
                return;
            }
            _scroll = GUILayout.BeginScrollView(_scroll, false, true, GUILayout.ExpandHeight(true));
            foreach ((string slot, DateTime time) in _slots)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(slot, GUILayout.Width(40));
                GUILayout.Label(time.ToString("yyyy/MM/dd HH:mm"), GUILayout.Width(130));
                SlotData.Set(slot, Note, GUILayout.TextField(SlotData.Get(slot, Note)));
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
        }

        // 遊戲的存檔、讀檔欄位填好標題後，把備註接在第一行標題後面
        public static void ShowNote(string slot, Text[] titles)
        {
            string note = SlotData.Get(slot, Note);
            if (note.Length > 0 && titles != null && titles.Length > 0 && titles[0] != null)
                titles[0].text += $"　〔{note}〕";
        }
    }
}
