using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Mortal.Battle;
using Mortal.Combat;
using Mortal.Core;
using Mortal.Story;
using UnityEngine;

namespace LegendOfMortalMod
{
    public class Patch
    {
        // 行動點無限：扣點都走 AddValue 負值；換旬與跳過本旬用 Set，不受影響
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameStat), "AddValue")]
        public static bool KeepActionPoints(GameStat __instance, int value)
        {
            return value >= 0 || __instance.StatType != GameStatType.行動次數 || !RoundControl.Unlimited;
        }

        // 骰子畫面開啟、擲骰、關閉，給 DiceControl 的指定按鈕用
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiceMenuDialog), "ExecuteRoll")]
        public static void DiceOpen(DiceMenuDialog __instance, string[] options, string checkName)
        {
            DiceControl.Begin(__instance, checkName, options);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiceMenuDialog), "Roll")]
        public static void DiceRoll(DiceMenuDialog __instance)
        {
            DiceControl.BeginRoll(__instance);
        }

        // 指定的擲骰跳過轉盤動畫
        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiceMenuDialog), "StartSpin")]
        public static bool DiceFastSpin(DiceMenuDialog __instance, ref IEnumerator __result)
        {
            if (!DiceControl.FastRoll(__instance))
                return true;
            __result = DiceControl.FastSpin(__instance);
            return false;
        }

        // 指定的擲骰出結果後不等「繼續」(也略過命運點重擲的詢問)
        [HarmonyPostfix]
        [HarmonyPatch(typeof(DiceMenuDialog), "GetNextInput")]
        public static void DiceFastContinue(DiceMenuDialog __instance, ref bool __result)
        {
            if (DiceControl.FastRolled(__instance))
                __result = true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(DiceMenuDialog), "OnDisable")]
        public static void DiceClose(DiceMenuDialog __instance)
        {
            DiceControl.End(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CheckPointManager), "Dice")]
        public static void BeforeDice(string name, ref int random)
        {
            DiceControl.BeforeDice(name, ref random);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CheckPointManager), "Dice")]
        public static void AfterDice(string name, DiceCheckResult __result)
        {
            DiceControl.AfterDice(name, __result);
        }

        // 練功 / 煉丹 / 鍛造面板的「最大消費」次數來源，面板打開時記下來
        private static readonly HashSet<StatConvertCollectionData> SpendLimitSources =
            new HashSet<StatConvertCollectionData>();

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MartialPanel), "Open")]
        public static void RecordMartialSpendLimit(MartialPanel __instance)
        {
            SpendLimitSources.Add(
                Traverse.Create(__instance).Field("_maxDataConvert").GetValue<StatConvertCollectionData>()
            );
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(UpgradePanel), "Open")]
        public static void RecordUpgradeSpendLimit(UpgradePanel __instance)
        {
            SpendLimitSources.Add(
                Traverse.Create(__instance).Field("_maxDataConvert").GetValue<StatConvertCollectionData>()
            );
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StatConvertCollectionData), "GetSummary")]
        public static bool ModifySpendLimit(StatConvertCollectionData __instance, ref float __result)
        {
            if (
                !Plugin.Instance.spendLimit
                || Plugin.Instance.spendLimitNumber <= 0
                || !SpendLimitSources.Contains(__instance)
            )
                return true;

            __result = Plugin.Instance.spendLimitNumber;
            return false;
        }

        // 遊戲讀過全域存檔才會存它 (LoadUniverseData 結尾也會存)；在那之前講經堂是空的，存了會蓋掉真的紀錄
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveSystem), "SaveUniverseData")]
        public static void UniverseSaved()
        {
            Plugin.Instance.universeLoaded = true;
        }

        // 左上角資源監視器 Profiler
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ProfilerRecorderController), "OnGUI")]
        public static bool ProfilerRecorderController()
        {
            return false;
        }

        // 戰役結束顯示滑鼠
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameLevelManager), "ContinueGame")]
        public static void ShowMouse(ref GameLevelManager __instance)
        {
            if (__instance.IsGameOver)
                Traverse.Create(__instance).Method("ShowMouseCursor").GetValue();
        }
    }
}
