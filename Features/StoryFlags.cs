using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Mortal.Core;
using Mortal.Story;
using UnityEngine;

namespace LegendOfMortalMod.Features
{
    // 劇情畫面左上的面板：這段劇情腳本會改哪些旗標、有哪些分支判定，判定條件依目前的值標成立/不成立；
    // 判定執行時也把結果和條件寫進回想（不寫入存檔）
    internal static class StoryFlags
    {
        private const string ScriptExtension = ".LuaScript";
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
        private static Vector2 _scroll;

        private static readonly Dictionary<string, string> Ops = new Dictionary<string, string>
        {
            { "Equal", "=" },
            { "NotEqual", "≠" },
            { "Less", "<" },
            { "LessEqual", "≤" },
            { "Greater", ">" },
            { "GreaterEqual", "≥" },
        };

        // Each story script as the game loads it; inline chunks (tmp…) run inside a script and keep its lists
        public static void ScriptLoaded(string luaString, string friendlyName)
        {
            string name = friendlyName ?? "";
            name = name.Substring(name.LastIndexOfAny(new[] { '/', '\\' }) + 1);
            if (
                !name.EndsWith(ScriptExtension, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("tmp", StringComparison.OrdinalIgnoreCase)
            )
                return;
            string lua = luaString ?? "";
            _flags = FlagCall.Matches(lua).Cast<Match>().GroupBy(match => match.Value).Select(g => g.First()).ToList();
            _checks = CheckCall
                .Matches(lua)
                .Cast<Match>()
                .GroupBy(match => match.Value)
                .Select(g => g.First())
                .ToList();
            _refreshAt = 0;
            _scroll = Vector2.zero;
        }

        // 在 OnGUI 裡、已套用選單縮放的座標下呼叫
        public static void Draw(float scale)
        {
            if (StoryManager.Instance == null || (_flags.Count == 0 && _checks.Count == 0))
                return;
            // 條件可能含隨機值，每幀重算會閃，所以隔一段時間才重建
            if (Time.unscaledTime >= _refreshAt)
            {
                _refreshAt = Time.unscaledTime + RefreshTime;
                _lines = Describe();
            }

            Rect rect = new Rect(10, 40, 460, Mathf.Min(Screen.height / scale - 60, 480));
            Color oldColor = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.85f);
            GUI.Box(rect, "");
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12));
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUIStyle style = new GUIStyle(GUI.skin.label) { richText = true };
            foreach (string line in _lines)
                GUILayout.Label(line, style);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.color = oldColor;
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
                        $"{(flag == null ? match.Groups[2].Value : CombatInfo.Flag(flag))} {op} {match.Groups[3].Value}{current}"
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
        private static string Check(bool isSwitch, string name)
        {
            object data = isSwitch ? SwitchData(name) : ConditionData(name);
            List<object> items = Plugin.Items(data, "_items").ToList();
            if (data == null || items.Count == 0)
                return $"{name}：（找不到條件）";
            string text = isSwitch ? $"分支 {name}" : $"條件 {name}";
            for (int i = 0; i < items.Count; i++)
            {
                bool ok = Traverse.Create(items[i]).Method("Check").GetValue() is true;
                string conditions = string.Join(
                    Traverse.Create(items[i]).Field("_logicOp").GetValue()?.ToString() == "OR" ? " 或 " : " 且 ",
                    Plugin.Items(items[i], "_items").Select(Compare)
                );
                string mark = ok ? "<color=#A3B86C>✔</color>" : "<color=#CD594A>✘</color>";
                text += $"\n    {mark} {(isSwitch ? $"{i + 1}. " : "")}{conditions}";
            }
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

        private static string Compare(object item)
        {
            string op = Traverse.Create(item).Field("_compareOp").GetValue()?.ToString() ?? "";
            return $"{Value(Traverse.Create(item).Field("_value1").GetValue<StatGroupVariable>())} "
                + $"{(Ops.TryGetValue(op, out string symbol) ? symbol : op)} "
                + Value(Traverse.Create(item).Field("_value2").GetValue<StatGroupVariable>());
        }

        // 常數只顯示數字，旗標顯示名稱和備註，其他顯示資料名稱；後面附目前的值
        private static string Value(StatGroupVariable variable)
        {
            if (variable == null)
                return "?";
            int result = variable.GetResult();
            if (variable.name.StartsWith("常數_"))
                return result.ToString();
            FlagData flag = Plugin
                .Items(variable, "_value")
                .Select(reference => Traverse.Create(reference).Field("_flag").GetValue<FlagData>())
                .FirstOrDefault(found => found != null);
            string label =
                flag != null && variable.name.StartsWith("旗標_")
                    ? CombatInfo.Flag(flag)
                    : Regex.Replace(variable.name, "^(數值|時間|旗標)_", "");
            return $"{label}({result})";
        }

        // 判定執行後寫進回想：結果和各分支的條件
        public static void Checked(bool isSwitch, string name, object result)
        {
            if (!Enabled)
                return;
            StoryLogData.AddText(
                $"<size=18><color=#BCA136>{Check(isSwitch, name)}\n    → 結果 {result}</color></size>",
                false
            );
        }
    }
}
