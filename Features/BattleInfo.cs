using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mortal.Battle;
using Mortal.Combat;
using Mortal.Core;
using UnityEngine;
using static LegendOfMortalMod.Features.GameText;
using SceneController = Mortal.Core.SceneController;

namespace LegendOfMortalMod.Features
{
    // 基本頁的戰鬥資訊：單挑的勝負旗標和依回合、數值等條件觸發的事件；團體戰的結果旗標和依秒數、人數觸發的事件
    internal static class BattleInfo
    {
        private static object _level;
        private static List<string> _lines;

        // 目前戰鬥的說明，不在戰鬥中時回傳 null；只在 Layout 時重建，免得 Layout 和 Repaint 畫的行數不同
        public static List<string> Lines()
        {
            if (SceneController.Instance != null && SceneController.Instance.IsLoading)
                return null;
            GameLevelManager battle = GameLevelManager.Instance;
            CombatManager combat =
                battle == null
                    ? Traverse.Create(typeof(CombatManager)).Property("Instance").GetValue<CombatManager>()
                    : null;
            object level =
                battle != null ? Traverse.Create(battle).Field("_levelData").GetValue()
                : combat != null ? Traverse.Create(combat).Field("_combatLevel").GetValue()
                : null;
            if (level == null)
                return null;
            if (Event.current.type == EventType.Layout || _lines == null)
            {
                if (!ReferenceEquals(level, _level))
                {
                    _level = level;
                    _lines = battle != null ? null : DescribeCombat(level);
                }
                if (battle != null)
                    _lines = DescribeBattle(battle, level);
            }
            if (combat != null && _lines != null && _lines.Count > 0)
                _lines[0] = $"單挑：目前第 {Traverse.Create(combat).Property("CurrentRounds").GetValue()} 回合";
            return _lines;
        }

        private static List<string> DescribeCombat(object level)
        {
            List<string> lines = new List<string>
            {
                "",
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

        // 團體戰的事件看秒數、人數和條件，觸發過一次就不再觸發；人數和秒數每次 Layout 都重算
        private static List<string> DescribeBattle(GameLevelManager battle, object level)
        {
            Traverse manager = Traverse.Create(battle);
            List<string> lines = new List<string>
            {
                $"團體戰：已過 {manager.Field("_currentSessionTime").GetValue()} 秒，"
                    + $"我方 {manager.Field("_currentFriendNpcs").GetValue()} 人、敵方 {manager.Field("_currentEnemyNpcs").GetValue()} 人",
            };
            if (Traverse.Create(level).Field("_victoryTimeout").GetValue() is true)
                lines.Add($"【限時】{Field(level, "_time")} 秒");
            foreach (string name in new[] { "_resultFlag", "_resultFlag2" })
            {
                FlagData flag = manager.Field(name).GetValue<FlagData>();
                if (flag != null)
                    lines.Add($"【結果寫入】{Flag(flag)}：0 陣亡、1 勝利、2 落敗、3 時間到");
            }

            lines.Add("【戰鬥中事件】");
            int count = 0;
            foreach (object data in Plugin.Items(level, "_events"))
            {
                List<string> triggers = new List<string>();
                if (Traverse.Create(data).Field("_checkTriggerSecond").GetValue() is true)
                    triggers.Add($"第 {Field(data, "_triggerSecond")} 秒");
                if (Traverse.Create(data).Field("_checkCountThreshold").GetValue() is true)
                    triggers.Add($"{Field(data, "_thresholdTeam")} 剩 {Field(data, "_countThreshold")} 人");
                object condition = Traverse.Create(data).Field("_condition").GetValue();
                if (Traverse.Create(data).Field("_checkCondition").GetValue() is true && condition != null)
                    triggers.AddRange(
                        Plugin
                            .Items(condition, "_items")
                            .Select(item => $"{Mark(Check(item))} {GameText.Conditions(item)}")
                    );
                List<string> actions = Plugin
                    .Items(data, "_actions")
                    .Select(BattleAction)
                    .Where(a => a != null)
                    .ToList();
                string done = Traverse.Create(data).Field("_hasTriggered").GetValue() is true ? "（已觸發）" : "";
                lines.Add(
                    $"{++count}. 當 {(triggers.Count == 0 ? "（無條件）" : string.Join(" 且 ", triggers))}{done}"
                        + $"\n    → {(actions.Count == 0 ? "（無）" : string.Join("、", actions))}"
                );
            }
            if (count == 0)
                lines.Add("（無）");
            return lines;
        }

        private static string BattleAction(object action)
        {
            switch (action.GetType().Name)
            {
                case "BattleEventActionSpawnReinforcement":
                    return $"{Field(action, "_team")} 援軍 {Field(action, "_count")} 人";
                case "BattleEventActionCallSupport":
                    return "援手攻擊";
                case "BattleEventActionFlashGameOver":
                    return Field(action, "_result") == "FriendWin" ? "直接判勝利" : "直接判落敗";
                case "BattleEventActionCinematic":
                    return null;
                default:
                    return action.GetType().Name.Replace("BattleEventAction", "");
            }
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
            string op = Op(Traverse.Create(condition).Field("_op").GetValue());
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

        private static string Name(object obj, string field) =>
            Traverse.Create(obj).Field(field).GetValue() is Object value && value != null ? value.name : "?";
    }
}
