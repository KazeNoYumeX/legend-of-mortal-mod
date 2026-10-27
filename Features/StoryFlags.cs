using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Mortal.Core;
using Mortal.Story;
using UnityEngine;
using static LegendOfMortalMod.Features.GameText;

namespace LegendOfMortalMod.Features
{
    // 基本頁的劇情旗標：這段劇情腳本會改哪些旗標、有哪些分支判定，判定條件依目前的值標成立/不成立；
    // 判定執行時也把結果和條件寫進回想（不寫入存檔）
    internal static class StoryFlags
    {
        private const float RefreshTime = 0.5f;
        private static readonly Regex FlagCall = new Regex(
            @"statmodifymanager\.(SetFlag|AddFlag)\(\s*""([^""]+)""\s*,\s*(-?\d+)\s*\)"
        );
        private static readonly Regex CheckCall = new Regex(
            @"checkpointmanager\.(Switch|Condition)\(\s*""([^""]+)""\s*\)"
        );

        public static bool Enabled;
        private static List<Match> _flags = new List<Match>();
        private static List<Match> _checks = new List<Match>();
        private static List<string> _lines = new List<string>();
        private static float _refreshAt;
        private static Dictionary<object, bool> _rolled;

        // Each story script as the game loads it (see StorySave.ScriptName)
        public static void ScriptLoaded(string luaString)
        {
            string lua = luaString ?? "";
            _flags = FlagCall.Matches(lua).Cast<Match>().GroupBy(match => match.Value).Select(g => g.First()).ToList();
            _checks = CheckCall
                .Matches(lua)
                .Cast<Match>()
                .GroupBy(match => match.Value)
                .Select(g => g.First())
                .ToList();
            _refreshAt = 0;
        }

        // 目前劇情腳本的說明，不在劇情中或沒有旗標時回傳 null；條件可能含隨機值，每幀重算會閃，所以隔一段時間、
        // 而且只在 Layout 時重建，免得 Layout 和 Repaint 畫的行數不同
        public static List<string> Lines()
        {
            if (StoryManager.Instance == null || (_flags.Count == 0 && _checks.Count == 0))
                return null;
            if (Event.current.type == EventType.Layout && Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + RefreshTime;
                _lines = Describe();
            }
            return _lines;
        }

        private static List<string> Describe()
        {
            List<string> lines = new List<string>();
            if (_flags.Count > 0)
            {
                lines.Add("【本段會改的旗標】");
                foreach (Match match in _flags)
                {
                    FlagData flag = MissionManagerData.Instance?.GetFlag(match.Groups[2].Value);
                    string op = match.Groups[1].Value == "SetFlag" ? "=" : "+=";
                    string current = flag == null ? "" : $"（目前 {flag.State}）";
                    lines.Add(
                        $"{(flag == null ? match.Groups[2].Value : Flag(flag))} {op} {match.Groups[3].Value}{current}"
                    );
                }
            }
            if (_checks.Count > 0)
            {
                lines.Add("【本段的分支判定】");
                foreach (Match match in _checks)
                    lines.Add(Check(match.Groups[1].Value == "Switch", match.Groups[2].Value));
            }
            return lines;
        }

        // Switch 依序檢查各分支，Condition 只看成立與否；每個分支一行，標出目前成立或不成立
        private static string Check(bool isSwitch, string name, Dictionary<object, bool> rolled = null)
        {
            object data = isSwitch ? SwitchData(name) : ConditionData(name);
            List<object> items = Plugin.Items(data, "_items").ToList();
            if (data == null || items.Count == 0)
                return $"{name}：（找不到條件）";
            string text = isSwitch ? $"分支 {name}" : $"條件 {name}";
            for (int i = 0; i < items.Count; i++)
                text +=
                    $"\n    {(rolled == null ? Mark(GameText.Check(items[i])) : rolled.TryGetValue(items[i], out bool ok) ? Mark(ok) : "—")} "
                    + $"{(isSwitch ? $"{i + 1}. " : "")}{Conditions(items[i])}";
            return text;
        }

        private static object SwitchData(string name)
        {
            CheckPointManager manager = CheckPointManager.Instance;
            if (manager == null)
                return null;
            foreach (object config in Plugin.Items(manager, "_switchList"))
            {
                if (config is SwitchResultConfig switches && switches.GetData(name) is SwitchResultData data)
                    return data;
            }
            return null;
        }

        private static object ConditionData(string name)
        {
            CheckPointManager manager = CheckPointManager.Instance;
            ConditionResultConfig config =
                manager == null ? null : Traverse.Create(manager).Field("_condition").GetValue<ConditionResultConfig>();
            return config?.List.FirstOrDefault(item => item != null && item.name == name);
        }

        // 判定時記下遊戲自己算出的每個分支結果（條件可能含隨機值，自己再算一次會不同），判定後和結果一起寫進回想。
        // 遊戲找到成立的分支就停，之後的分支沒算過，標「—」
        public static void BeforeCheck() => _rolled = Enabled ? new Dictionary<object, bool>() : null;

        public static void Rolled(object item, bool result)
        {
            if (_rolled != null && !_rolled.ContainsKey(item))
                _rolled[item] = result;
        }

        public static void Checked(bool isSwitch, string name, object result)
        {
            if (_rolled == null)
                return;
            string text = Check(isSwitch, name, _rolled);
            _rolled = null;
            StoryLogData.AddText($"<size=18><color=#BCA136>{text}\n    → 結果 {result}</color></size>", false);
        }
    }
}
