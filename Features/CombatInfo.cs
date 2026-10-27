using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mortal.Combat;
using Mortal.Core;
using UnityEngine;
using SceneController = Mortal.Core.SceneController;

namespace LegendOfMortalMod.Features
{
    // 單挑畫面左上的資料面板：勝利/落敗會改哪些旗標，以及關卡裡依回合、數值等條件觸發的事件
    internal static class CombatInfo
    {
        private static object _level;
        private static List<string> _lines;
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

        // 在 OnGUI 裡、已套用選單縮放的座標下呼叫
        public static void Draw(float scale)
        {
            if (SceneController.Instance != null && SceneController.Instance.IsLoading)
                return;
            CombatManager combat = Traverse
                .Create(typeof(CombatManager))
                .Property("Instance")
                .GetValue<CombatManager>();
            object level = combat == null ? null : Traverse.Create(combat).Field("_combatLevel").GetValue();
            if (level == null)
                return;
            if (!ReferenceEquals(level, _level))
            {
                _level = level;
                _lines = Describe(level);
                _scroll = Vector2.zero;
            }

            Rect rect = new Rect(10, 40, 420, Mathf.Min(Screen.height / scale - 60, 480));
            Color oldColor = GUI.color;
            GUI.color = new Color(1, 1, 1, 0.85f);
            GUI.Box(rect, "");
            GUILayout.BeginArea(new Rect(rect.x + 8, rect.y + 6, rect.width - 16, rect.height - 12));
            GUILayout.Label($"目前第 {Traverse.Create(combat).Property("CurrentRounds").GetValue()} 回合");
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (string line in _lines)
                GUILayout.Label(line);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
            GUI.color = oldColor;
        }

        private static List<string> Describe(object level)
        {
            List<string> lines = new List<string>
            {
                "【勝利】" + Flags(Plugin.Items(level, "_winResult")),
                "【落敗】" + Flags(Plugin.Items(level, "_loseResult")),
            };
            foreach (object result in Plugin.Items(level, "_conditionResult"))
                lines.Add($"【結算時若 {Conditions(result)}】{Flags(Plugin.Items(result, "_flags"))}");

            lines.Add("【戰鬥中事件】");
            int count = 0;
            foreach (object data in Plugin.Items(level, "Events"))
            foreach (object item in Plugin.Items(data, "_conditionAction"))
            {
                List<string> actions = new[] { "_actions", "_afterActions", "_afterResult" }
                    .SelectMany(list => Plugin.Items(item, list))
                    .Select(Action)
                    .Where(action => action != null)
                    .ToList();
                lines.Add(
                    $"{++count}. 當 {Conditions(item)}\n    → {(actions.Count == 0 ? "（無）" : string.Join("、", actions))}"
                );
            }
            if (count == 0)
                lines.Add("（無）");
            return lines;
        }

        private static string Flags(IEnumerable<object> flags)
        {
            List<string> list = flags
                .Select(flag =>
                    Flag(Traverse.Create(flag).Field("_flag").GetValue<FlagData>()) + " = " + Field(flag, "_state")
                )
                .ToList();
            return list.Count == 0 ? "不改旗標" : string.Join("、", list);
        }

        private static string Conditions(object item)
        {
            List<string> list = Plugin.Items(item, "_conditions").Select(Condition).ToList();
            if (list.Count == 0)
                return "（無條件）";
            return string.Join(Field(item, "_logic") == "OR" ? " 或 " : " 且 ", list);
        }

        private static string Condition(object condition)
        {
            string op = Ops.TryGetValue(Field(condition, "_op"), out string symbol) ? symbol : Field(condition, "_op");
            switch (condition.GetType().Name)
            {
                case "CombatEventConditionRound":
                    return $"回合 {op} {Field(condition, "_round")}";
                case "CombatEventConditionRoundFlag":
                    return $"回合 {op} {Flag(Traverse.Create(condition).Field("_flagData").GetValue<FlagData>())}";
                case "CombatEventConditionStat":
                    return $"{Field(condition, "_character")}{Field(condition, "_type")} {op} {Field(condition, "_value")}";
                case "CombatEventConditionFlag":
                    return $"{Flag(Traverse.Create(condition).Field("_flagData").GetValue<FlagData>())} {op} {Field(condition, "_value")}";
                case "CombatEventConditionPlayerStat":
                    return $"主角{Field(Traverse.Create(condition).Field("_stat").GetValue(), "_checkType")} {op} {Field(condition, "_value")}";
                case "CombatEventConditionActionType":
                    return $"{Field(condition, "_character")}出招為 {string.Join("/", Plugin.Items(condition, "_typeList"))}";
                case "CombatEventConditionActionResult":
                    return $"{Field(condition, "_character")}行動{Field(condition, "_type")}";
                case "CombatEventConditionEffectState":
                    return $"{Field(condition, "_character")}有狀態 {Name(condition, "_effectState")}";
                case "CombatEventConditionBlockCounterAttack":
                    return $"{Field(condition, "_character")}格檔 {Field(condition, "_blockResult")}";
                default:
                    return condition.GetType().Name.Replace("CombatEventCondition", "");
            }
        }

        private static string Action(object action)
        {
            switch (action.GetType().Name)
            {
                case "CombatEventActionDialog":
                    return "對話";
                case "CombatEventActionLoadScene":
                    return Field(action, "_type") == "指定場景"
                        ? $"結束戰鬥（{Field(action, "_sceneName")}）"
                        : "結束戰鬥";
                case "CombatEventActionSetStoryScript":
                    return $"之後劇情改為 {Field(action, "_name")}";
                case "CombatEventActionModifyFlag":
                    return $"{Flag(Traverse.Create(action).Field("_flag").GetValue<FlagData>())} = {Field(action, "_state")}";
                case "CombatEventActionFlagRound":
                    return $"{Flag(Traverse.Create(action).Field("_flag").GetValue<FlagData>())} += {Field(action, "_add")}";
                case "CombatEventActionModifyStat":
                    string percent = Field(action, "_valueType") == "百分比" ? "%" : "";
                    return $"{Field(action, "_character")}{Field(action, "_type")} {Field(action, "_value")}{percent}";
                case "CombatEventActionStateEffect":
                    return $"{Field(action, "_character")}{Field(action, "_type")}狀態 {Name(action, "_effect")}";
                case "CombatEventActionRemoveAllStateEffect":
                    return $"{Field(action, "_character")}移除所有狀態";
                case "CombatEventActionRemoveAllSupport":
                    return $"{Field(action, "_character")}移除所有援手";
                case "CombatEventActionPunchSupport":
                    return $"{Field(action, "_character")}援手 {Name(action, "_support")}";
                case "CombatEventActionRelationshipAttack":
                    return $"{Field(action, "_character")}好感攻擊";
                case "CombatEventActionCrazyMode":
                    return $"{Field(action, "_character")}{Field(action, "_mode")}";
                case "CombatEventActionShowImage":
                case "CombatEventActionHideImage":
                case "CombatEventActionVFX":
                case "CombatEventActionDebug":
                    return null;
                default:
                    return action.GetType().Name.Replace("CombatEventAction", "");
            }
        }

        private static string Flag(FlagData flag) =>
            flag == null ? "?"
            : string.IsNullOrEmpty(flag.DevNote) ? flag.name
            : $"{flag.name}（{flag.DevNote}）";

        private static string Field(object obj, string field) =>
            obj == null ? "" : Traverse.Create(obj).Field(field).GetValue()?.ToString() ?? "";

        private static string Name(object obj, string field) =>
            Traverse.Create(obj).Field(field).GetValue() is Object value && value != null ? value.name : "?";
    }
}
